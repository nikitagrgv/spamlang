using System.Diagnostics;

namespace Compiler;

public class IRGen
{
    private readonly string _code;
    private readonly Diagnostic _diag;
    private readonly IReadOnlyList<Token> _tokens;
    private readonly List<Dictionary<Symbol, IRValue>> _symbolScopes = new();

    public IRGen(string code, List<Token> tokens, Diagnostic diag)
    {
        _code = code;
        _diag = diag;
        _tokens = tokens;
    }

    public IRModule Run(CompilationUnit unit)
    {
        Dictionary<Symbol, IRValue> globalScope = new();
        _symbolScopes.Add(globalScope);

        List<IRFunction> functions = new();
        foreach (FuncDecl funcDecl in unit.FuncDecls)
        {
            Debug.Assert(funcDecl.Symbol is { Type: FuncType });
            FuncType funcType = (FuncType)funcDecl.Symbol.Type;
            IRFunction func = new()
            {
                BasicBlocks = new List<IRBasicBlock>(),
                Name = funcDecl.Symbol.Name,
                Params = new List<IRParam>(),
                Signature = funcType,
            };
            globalScope.Add(funcDecl.Symbol, func);
            functions.Add(func);
        }

        for (int i = 0; i < unit.FuncDecls.Count; i++)
        {
            FuncDecl funcDecl = unit.FuncDecls[i];
            IRFunction function = functions[i];
            GenFunction(funcDecl, function);
        }

        IRModule module = new()
        {
            Functions = functions,
        };

        _symbolScopes.RemoveAt(_symbolScopes.Count - 1);
        return module;
    }

    private void GenFunction(FuncDecl funcDecl, IRFunction function)
    {
        // TODO: Reuse lists/dicts
        List<StmtLet> locals = new();
        CollectLocals(funcDecl.Body, locals);

        IRBasicBlock entry = new()
        {
            Instructions = new List<IRInstruction>(),
            Name = "entry",
        };
        function.BasicBlocks.Add(entry);

        Dictionary<Symbol, IRValue> funcScope = new();
        _symbolScopes.Add(funcScope);

        GenParams(entry, funcDecl.Params, function.Params);
        GenLocals(entry, locals);

        GenBlock(entry, funcDecl.Body);

        if (function.Signature.ReturnType == BuiltinType.Void &&
            (entry.Instructions.Count == 0 || !entry.Instructions[^1].IsTerminator))
        {
            IRInstructionRet ret = new()
            {
                Value = null
            };
            entry.Add(ret);
        }

        _symbolScopes.RemoveAt(_symbolScopes.Count - 1);
    }

    private void GenBlock(IRBasicBlock block, Block blockNode)
    {
        foreach (Stmt stmt in blockNode.Stmts)
        {
            switch (stmt)
            {
                case Block b:
                    GenBlock(block, b);
                    break;
                case StmtAssign stmtAssign:
                    GenStmtAssign(block, stmtAssign);
                    break;
                case StmtExpr stmtExpr:
                    GenStmtExpr(block, stmtExpr);
                    break;
                case StmtLet stmtLet:
                    GenStmtLet(block, stmtLet);
                    break;
                case StmtReturn stmtReturn:
                    GenStmtReturn(block, stmtReturn);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stmt));
            }
        }
    }

    private void GenStmtAssign(IRBasicBlock block, StmtAssign stmtAssign)
    {
        IRValue value = GenExprValue(block, stmtAssign.Value);
        IRValue addr = GenExprAddr(block, stmtAssign.Target);
        IRInstructionStore store = new()
        {
            Value = value,
            Address = addr,
        };
        block.Add(store);
    }

    private void GenStmtExpr(IRBasicBlock block, StmtExpr stmtExpr)
    {
        GenExprValue(block, stmtExpr.Expr);
    }

    private void GenStmtLet(IRBasicBlock block, StmtLet stmtLet)
    {
        Debug.Assert(stmtLet.Symbol != null);

        IRValue value;
        if (stmtLet.Expr != null)
        {
            value = GenExprValue(block, stmtLet.Expr);
        }
        else
        {
            value = MakeZeroInitialized(stmtLet.Symbol.Type);
        }

        IRValue? addr = LookupValue(stmtLet.Symbol);
        Debug.Assert(addr != null);

        IRInstructionStore store = new()
        {
            Value = value,
            Address = addr,
        };
        block.Add(store);
    }

    private void GenStmtReturn(IRBasicBlock block, StmtReturn stmtReturn)
    {
        IRValue? value = null;
        if (stmtReturn.Expr != null)
        {
            value = GenExprValue(block, stmtReturn.Expr);
        }

        IRInstructionRet ret = new()
        {
            Value = value,
        };
        block.Add(ret);
    }

    private IRValue GenExprValue(IRBasicBlock block, Expr expr)
    {
        Debug.Assert(expr.ResolvedType != null);
        Debug.Assert(expr.ValueCategory != null);

        if (expr.ValueCategory == ValueCategory.LValue)
        {
            IRValue addr = GenExprAddr(block, expr);
            IRInstructionLoad load = new()
            {
                LoadedType = expr.ResolvedType,
                Address = addr,
            };
            block.Add(load);
            return load;
        }

        switch (expr)
        {
            case ExprBinary exprBinary:
                return GenExprBinaryValue(block, exprBinary);
            case ExprCall exprCall:
                return GenExprCallValue(block, exprCall);
            case ExprImplicitCast exprImplicitCast:
                return GenExprImplicitCastValue(block, exprImplicitCast);
            case ExprIdentifier exprIdentifier:
                return GenExprIdentifierValue(exprIdentifier);
            case ExprInt exprInt:
                return GenExprIntValue(exprInt);
            case ExprUnary exprUnary:
                return GenExprUnaryValue(block, exprUnary);
            default:
                throw new ArgumentOutOfRangeException(nameof(expr));
        }
    }

    private IRValue GenExprBinaryValue(IRBasicBlock block, ExprBinary expr)
    {
        IRValue left = GenExprValue(block, expr.Left);
        IRValue right = GenExprValue(block, expr.Right);
        TokenType opTokType = _tokens[expr.OperatorToken].Type;
        return GenBinaryOp(block, left, right, opTokType);
    }

    private IRValue GenExprUnaryValue(IRBasicBlock block, ExprUnary expr)
    {
        IRValue operand = GenExprValue(block, expr.Expr);
        TokenType opTokType = _tokens[expr.OperatorToken].Type;
        switch (opTokType)
        {
            case TokenType.Minus:
            case TokenType.Plus:
                IRValue zero = MakeZeroInitialized(operand.Type);
                return GenBinaryOp(block, zero, operand, opTokType);
            default:
                throw new UnreachableException();
        }
    }

    private IRValue GenBinaryOp(IRBasicBlock block, IRValue left, IRValue right, TokenType tokenType)
    {
        Debug.Assert(left.Type == right.Type);

        bool signed;
        if (left.Type == BuiltinType.I32)
        {
            signed = true;
        }
        else
        {
            throw new UnreachableException();
        }

        IRBinaryOp op;
        switch (tokenType)
        {
            case TokenType.Plus: op = IRBinaryOp.Add; break;
            case TokenType.Minus: op = IRBinaryOp.Sub; break;
            case TokenType.Star: op = IRBinaryOp.Mul; break;
            case TokenType.Slash: op = signed ? IRBinaryOp.SDiv : IRBinaryOp.UDiv; break;
            case TokenType.Percent: op = signed ? IRBinaryOp.SRem : IRBinaryOp.URem; break;
            default: throw new UnreachableException();
        }

        IRInstructionBinary instr = new()
        {
            Left = left,
            Right = right,
            Op = op,
        };
        block.Add(instr);
        return instr;
    }

    private IRValue GenExprCallValue(IRBasicBlock block, ExprCall expr)
    {
        IRValue callee = GenExprValue(block, expr.Callee);

        List<IRValue> args = new();
        foreach (Expr arg in expr.Args)
        {
            IRValue argValue = GenExprValue(block, arg);
            args.Add(argValue);
        }

        Debug.Assert(expr.Callee.ResolvedType is FuncType);

        IRInstructionCall call = new()
        {
            Args = args,
            Callee = callee,
            Signature = (FuncType)expr.Callee.ResolvedType,
        };
        block.Add(call);
        return call;
    }

    private IRValue GenExprImplicitCastValue(IRBasicBlock block, ExprImplicitCast expr)
    {
        IRValue value = GenExprValue(block, expr.Operand);
        IRInstructionCast cast = new()
        {
            Value = value,
            CastTo = expr.Target
        };
        block.Add(cast);
        return cast;
    }

    private IRValue GenExprIdentifierValue(ExprIdentifier expr)
    {
        Debug.Assert(expr.Symbol != null);
        Debug.Assert(expr.Symbol is FuncSymbol, "Variables can't reach here (other are lvalues)");

        Symbol sym = expr.Symbol;
        IRValue? value = LookupValue(sym);
        Debug.Assert(value != null);
        return value;
    }

    private IRValue GenExprIntValue(ExprInt expr)
    {
        Debug.Assert(expr.ResolvedType != null);

        IRConstantInt value = new()
        {
            Value = expr.Value,
            IntType = expr.ResolvedType,
        };
        return value;
    }

    private IRValue GenExprAddr(IRBasicBlock block, Expr expr)
    {
        Debug.Assert(expr.ResolvedType != null);
        Debug.Assert(expr.ValueCategory == ValueCategory.LValue);
        switch (expr)
        {
            case ExprIdentifier exprIdentifier:
                Debug.Assert(exprIdentifier.Symbol != null);
                Symbol sym = exprIdentifier.Symbol;
                IRValue? value = LookupValue(sym);
                Debug.Assert(value != null);
                return value;
            default:
                throw new UnreachableException();
        }
    }

    private IRValue MakeZeroInitialized(Type type)
    {
        // TODO: Don't allocate, put in static fields
        if (type == BuiltinType.I32 || type == BuiltinType.Ptr)
        {
            return new IRConstantInt
            {
                Value = 0,
                IntType = type,
            };
        }

        throw new NotImplementedException();
    }

    private void GenLocals(IRBasicBlock entry, List<StmtLet> locals)
    {
        foreach (StmtLet let in locals)
        {
            Debug.Assert(let.Symbol != null);

            Symbol sym = let.Symbol;
            Type type = sym.Type;

            IRInstructionAlloca alloca = new()
            {
                AllocatedType = type,
            };
            entry.Add(alloca);

            Debug.Assert(LookupValue(sym) == null);
            CurrentScope.Add(sym, alloca);
        }
    }

    private void GenParams(IRBasicBlock entry, IReadOnlyList<Param> funcParams, List<IRParam> irParams)
    {
        Debug.Assert(irParams.Count == 0);

        int initialNumInstructions = entry.Instructions.Count;
        foreach (Param param in funcParams)
        {
            Debug.Assert(param.Symbol != null);
            Symbol sym = param.Symbol;

            IRParam irParam = new()
            {
                ParamType = sym.Type,
                Index = irParams.Count,
            };
            irParams.Add(irParam);

            IRInstructionAlloca alloca = new()
            {
                AllocatedType = sym.Type,
            };
            entry.Add(alloca);

            Debug.Assert(LookupValue(sym) == null);
            CurrentScope.Add(sym, alloca);
        }

        for (int i = 0; i < irParams.Count; i++)
        {
            IRParam param = irParams[i];
            IRInstruction alloca = entry.Instructions[initialNumInstructions + i];
            Debug.Assert(alloca is IRInstructionAlloca);
            IRInstructionStore store = new()
            {
                Value = param,
                Address = alloca
            };
            entry.Add(store);
        }
    }

    private void CollectLocals(Block body, List<StmtLet> locals)
    {
        foreach (Stmt stmt in body.Stmts)
        {
            if (stmt is StmtLet let)
            {
                locals.Add(let);
            }
            else if (stmt is Block block)
            {
                CollectLocals(block, locals);
            }
        }
    }

    private Dictionary<Symbol, IRValue> CurrentScope => _symbolScopes[^1];

    private IRValue? LookupValue(Symbol symbol)
    {
        for (int i = _symbolScopes.Count - 1; i >= 0; --i)
        {
            Dictionary<Symbol, IRValue> scope = _symbolScopes[i];
            if (scope.TryGetValue(symbol, out IRValue? value))
            {
                return value;
            }
        }

        return null;
    }
}
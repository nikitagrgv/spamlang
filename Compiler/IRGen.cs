using System.Diagnostics;

namespace Compiler;

public class IRGen
{
    private readonly string _code;
    private readonly Diagnostic _diag;
    private readonly IReadOnlyList<Token> _tokens;

    public IRGen(string code, List<Token> tokens, Diagnostic diag)
    {
        _code = code;
        _diag = diag;
        _tokens = tokens;
    }

    public IRModule Run(CompilationUnit unit)
    {
        List<IRFunction> functions = new();

        foreach (FuncDecl funcDecl in unit.FuncDecls)
        {
            IRFunction func = GenFunction(funcDecl);
            functions.Add(func);
        }

        IRModule module = new()
        {
            Functions = functions,
        };
        return module;
    }

    private IRFunction GenFunction(FuncDecl funcDecl)
    {
        // TODO: Reuse lists/dicts
        Debug.Assert(funcDecl.Symbol is { Type: FuncType });
        FuncType funcType = (FuncType)funcDecl.Symbol.Type;

        List<StmtLet> locals = new();
        CollectLocals(funcDecl.Body, locals);

        List<IRBasicBlock> basicBlocks = new();
        IRBasicBlock entry = new()
        {
            Instructions = new List<IRInstruction>(),
            Name = "entry",
        };
        basicBlocks.Add(entry);

        Dictionary<Symbol, IRValue> variableToValue = new();
        List<IRParam> irParams = new();
        GenParams(entry, funcDecl.Params, irParams, variableToValue);
        GenLocals(entry, locals, variableToValue);

        GenBlock(entry, funcDecl.Body, variableToValue);

        return new IRFunction
        {
            BasicBlocks = basicBlocks,
            Name = funcDecl.Symbol.Name,
            Params = irParams,
            Type = funcType,
        };
    }

    private void GenBlock(IRBasicBlock block, Block blockNode, IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
        foreach (Stmt stmt in blockNode.Stmts)
        {
            switch (stmt)
            {
                case Block b:
                    GenBlock(block, b, variableToValue);
                    break;
                case StmtAssign stmtAssign:
                    GenStmtAssign(block, stmtAssign, variableToValue);
                    break;
                case StmtExpr stmtExpr:
                    GenStmtExpr(block, stmtExpr, variableToValue);
                    break;
                case StmtLet stmtLet:
                    GenStmtLet(block, stmtLet, variableToValue);
                    break;
                case StmtReturn stmtReturn:
                    GenStmtReturn(block, stmtReturn, variableToValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stmt));
            }
        }
    }

    private void GenStmtAssign(IRBasicBlock block, StmtAssign stmtAssign,
        IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
    }

    private void GenStmtExpr(IRBasicBlock block, StmtExpr stmtExpr,
        IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
    }

    private void GenStmtLet(IRBasicBlock block, StmtLet stmtLet, IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
    }

    private void GenStmtReturn(IRBasicBlock block, StmtReturn stmtReturn,
        IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
        IRValue? value = null;
        if (stmtReturn.Expr != null)
        {
            value = GenExprValue(block, stmtReturn.Expr, variableToValue);
        }

        IRInstructionRet ret = new()
        {
            Value = value,
        };
        block.Add(ret);
    }

    private IRValue GenExprValue(IRBasicBlock block, Expr expr, IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
        Debug.Assert(expr.ResolvedType != null);
        Debug.Assert(expr.ValueCategory != null);

        if (expr.ValueCategory == ValueCategory.LValue)
        {
            IRValue addr = GenExprAddr(block, expr, variableToValue);
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
                return GenExprBinaryValue(block, exprBinary, variableToValue);
                break;
            case ExprCall exprCall:
                return GenExprCallValue(block, exprCall, variableToValue);
                break;
            case ExprImplicitCast exprImplicitCast:
                return GenExprImplicitCastValue(block, exprImplicitCast, variableToValue);
                break;
            case ExprIdentifier exprIdentifier:
                return GenExprIdentifierValue(block, exprIdentifier, variableToValue);
                break;
            case ExprInt exprInt:
                return GenExprIntValue(block, exprInt, variableToValue);
                break;
            case ExprUnary exprUnary:
                return GenExprUnaryValue(block, exprUnary, variableToValue);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expr));
        }
    }

    private IRValue GenExprBinaryValue(IRBasicBlock block, ExprBinary expr,
        IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
    }

    private IRValue GenExprCallValue(IRBasicBlock block, ExprCall expr,
        IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
    }

    private IRValue GenExprImplicitCastValue(IRBasicBlock block, ExprImplicitCast expr,
        IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
    }

    private IRValue GenExprIdentifierValue(IRBasicBlock block, ExprIdentifier expr,
        IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
    }

    private IRValue GenExprIntValue(IRBasicBlock block, ExprInt expr,
        IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
    }

    private IRValue GenExprUnaryValue(IRBasicBlock block, ExprUnary expr,
        IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
    }

    private IRValue GenExprAddr(IRBasicBlock block, Expr expr, IReadOnlyDictionary<Symbol, IRValue> variableToValue)
    {
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

    private void GenLocals(IRBasicBlock entry, List<StmtLet> locals, Dictionary<Symbol, IRValue> variableToValue)
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

            Debug.Assert(!variableToValue.ContainsKey(sym));
            variableToValue.Add(sym, alloca);
        }
    }

    private void GenParams(IRBasicBlock entry, IReadOnlyList<Param> funcParams, List<IRParam> irParams,
        Dictionary<Symbol, IRValue> variableToValue)
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

            Debug.Assert(!variableToValue.ContainsKey(sym));
            variableToValue.Add(sym, alloca);
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
}
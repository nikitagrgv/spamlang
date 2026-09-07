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

        List<IRParam> irParams = new();
        foreach (Param param in funcDecl.Params)
        {
            Debug.Assert(param.Type.ResolvedType != null);
            IRParam p = new()
            {
                ParamType = param.Type.ResolvedType,
                Index = irParams.Count,
            };
            irParams.Add(p);
        }

        List<StmtLet> locals = new();
        CollectLocals(funcDecl.Body, locals);

        List<IRBasicBlock> basicBlocks = new();
        IRBasicBlock entry = new()
        {
            Instructions = new List<IRInstruction>(),
            Name = "entry",
        };
        basicBlocks.Add(entry);

        GenParams(entry, irParams);

        Dictionary<Symbol, IRValue> localToValue = new();
        GenLocals(entry, locals, localToValue);

        GenBody(entry, funcDecl.Body, localToValue);

        return new IRFunction
        {
            BasicBlocks = basicBlocks,
            Name = funcDecl.Symbol.Name,
            Params = irParams,
            Type = funcType,
        };
    }

    private void GenBody(IRBasicBlock entry, Block body, IReadOnlyDictionary<Symbol, IRValue> localToValue)
    {
        foreach (Stmt stmt in body.Stmts)
        {
            switch (stmt)
            {
                case Block block:
                    GenBody(entry, block, localToValue);
                    break;
                case StmtAssign stmtAssign:
                    GenStmtAssign(entry, stmtAssign, localToValue);
                    break;
                case StmtExpr stmtExpr:
                    GenStmtExpr(entry, stmtExpr, localToValue);
                    break;
                case StmtLet stmtLet:
                    GenStmtLet(entry, stmtLet, localToValue);
                    break;
                case StmtReturn stmtReturn:
                    GenStmtReturn(entry, stmtReturn, localToValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stmt));
            }
        }
    }

    private void GenStmtAssign(IRBasicBlock entry, StmtAssign stmtAssign,
        IReadOnlyDictionary<Symbol, IRValue> localToValue)
    {
    }

    private void GenStmtExpr(IRBasicBlock entry, StmtExpr stmtExpr, IReadOnlyDictionary<Symbol, IRValue> localToValue)
    {
    }

    private void GenStmtLet(IRBasicBlock entry, StmtLet stmtLet, IReadOnlyDictionary<Symbol, IRValue> localToValue)
    {
    }

    private void GenStmtReturn(IRBasicBlock entry, StmtReturn stmtReturn,
        IReadOnlyDictionary<Symbol, IRValue> localToValue)
    {
        IRValue? value = null;
        if (stmtReturn.Expr != null)
        {
            value = GenExpr(entry, stmtReturn.Expr, localToValue);
        }

        IRInstructionRet ret = new()
        {
            Value = value,
        };
        entry.Add(ret);
    }

    private IRValue GenExpr(IRBasicBlock entry, Expr expr, IReadOnlyDictionary<Symbol, IRValue> localToValue)
    {
        Debug.Assert(expr.ResolvedType != null);
        // TODO#
        return MakeZeroInitialized(expr.ResolvedType);
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

    private void GenLocals(IRBasicBlock entry, List<StmtLet> locals, Dictionary<Symbol, IRValue> localToValue)
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

            Debug.Assert(!localToValue.ContainsKey(sym));
            localToValue.Add(sym, alloca);
        }
    }

    private void GenParams(IRBasicBlock entry, List<IRParam> irParams)
    {
        int initialNumInstructions = entry.Instructions.Count;
        foreach (IRParam param in irParams)
        {
            IRInstructionAlloca alloca = new()
            {
                AllocatedType = param.ParamType,
            };
            entry.Add(alloca);
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
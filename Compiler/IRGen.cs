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

        return new IRFunction
        {
            BasicBlocks = basicBlocks,
            Name = funcDecl.Symbol.Name,
            Params = irParams,
            Type = funcType,
        };
    }

    private void GenLocals(IRBasicBlock entry, List<StmtLet> locals, Dictionary<Symbol, IRValue> localToValue)
    {
        foreach (StmtLet let in locals)
        {
            Debug.Assert(let.Symbol != null);
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
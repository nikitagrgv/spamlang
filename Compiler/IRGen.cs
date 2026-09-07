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
        // TODO: Reuse lists
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

        return new IRFunction
        {
            BasicBlocks = basicBlocks,
            Name = funcDecl.Symbol.Name,
            Params = irParams,
            Type = funcType,
        };
    }

    private void CollectLocals(Block body, List<StmtLet> locals)
    {
    }
}
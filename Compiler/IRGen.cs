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
    }
}
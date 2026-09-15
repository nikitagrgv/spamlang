namespace Spamlang.Frontend;

public class Frontend
{
    public struct Result
    {
        public List<Token> Tokens { get; init; }
        public CompilationUnit CompilationUnit { get; init; }
    }

    public static Result Run(string code, TypeRegistry typeRegistry, Diagnostic diag, Timers timers)
    {
        timers.RestartTimer();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);
        timers.FinishTimer("Lexer");

        timers.RestartTimer();
        Parser parser = new();
        CompilationUnit compilationUnit = parser.Run(code, tokens, diag);
        timers.FinishTimer("Parser");

        timers.RestartTimer();
        Sema sema = new(code, tokens, diag, typeRegistry);
        sema.Run(compilationUnit);
        timers.FinishTimer("Sema");

        return new Result
        {
            Tokens = tokens,
            CompilationUnit = compilationUnit,
        };
    }
}
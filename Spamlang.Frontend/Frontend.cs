namespace Spamlang.Frontend;

public class Frontend
{
    public struct Result
    {
        public required List<Token> Tokens { get; init; }
        public required CompilationUnit CompilationUnit { get; init; }
        public required HIRCompilationUnit HIRCompilationUnit { get; init; }
    }

    public static Result Run(string code, TypeRegistry typeRegistry, Diagnostic diag, Timers? timers, Dictionary<int, Symbol>? outTokenToSymbol = null)
    {
        timers?.RestartTimer();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);
        timers?.FinishTimer("Lexer");

        timers?.RestartTimer();
        Parser parser = new();
        CompilationUnit compilationUnit = parser.Run(code, tokens, diag);
        timers?.FinishTimer("Parser");

        timers?.RestartTimer();
        Sema sema = new(code, tokens, diag, typeRegistry);
        HIRCompilationUnit typedCompilationUnit = sema.Run(compilationUnit, outTokenToSymbol);
        timers?.FinishTimer("Sema");

        return new Result
        {
            Tokens = tokens,
            CompilationUnit = compilationUnit,
            HIRCompilationUnit = typedCompilationUnit,
        };
    }
}
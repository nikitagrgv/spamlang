using Spamlang.DebugPrinters;
using Spamlang.Frontend;

namespace Tests;

public class ParserTest
{
    private static bool HasErrors(string code)
    {
        (_, _, Diagnostic diag) = Parse(code);
        return diag.HasErrors;
    }

    private static string CodeToFlatAst(string code)
    {
        (CompilationUnit unit, List<Token> tokens, Diagnostic _) = Parse(code);
        return ToFlatAst(unit, tokens, code);
    }

    private static (CompilationUnit, List<Token>, Diagnostic) Parse(string code)
    {
        Diagnostic diag = new();
        Lexer lexer = new(code, diag);
        List<Token> tokens = lexer.Run();
        Parser parser = new();
        CompilationUnit unit = parser.Run(code, tokens, diag);
        return (unit, tokens, diag);
    }

    private static string ToFlatAst(CompilationUnit unit, List<Token> tokens, string code)
    {
        AstPrinter printer = new(tokens, code);
        printer.PrettyExpr();
    }

    [Theory]
    [InlineData(" ", " ")]
    public void Parser_Parses(string code, string expected)
    {
        string parsed = CodeToFlatAst(code);
        Assert.Equal(expected, parsed);
    }
}
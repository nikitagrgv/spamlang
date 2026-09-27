using Spamlang.Frontend;

namespace Tests;

public class ParserTest
{
    private static bool HasErrors(string code)
    {
        (CompilationUnit _, Diagnostic diag) = Parse(code);
        return diag.HasErrors;
    }

    private static string CodeToFlatAst(string code)
    {
        (CompilationUnit unit, Diagnostic _) = Parse(code);
        return ToFlatAst(unit, code);
    }

    private static (CompilationUnit, Diagnostic) Parse(string code)
    {
        Diagnostic diag = new();
        Lexer lexer = new(code, diag);
        List<Token> tokens = lexer.Run();
        Parser parser = new();
        CompilationUnit unit = parser.Run(code, tokens, diag);
        return (unit, diag);
    }

    private static string ToFlatAst(CompilationUnit unit, string code)
    {
    }


    [Theory]
    [InlineData(" ", " ")]
    public void Parser_Parses(string code, string expected)
    {
        string parsed = CodeToFlatAst(code);
        Assert.Equal(expected, parsed);
    }
}
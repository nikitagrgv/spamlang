using Spamlang.DebugPrinters;
using Spamlang.Frontend;

namespace Tests;

public class ParserTest
{
    private static bool ExprHasErrors(string code)
    {
        (_, _, Diagnostic diag) = ParseExpr(code);
        return diag.HasErrors;
    }

    private static string ExprCodeToFlatAst(string code)
    {
        (Expr expr, List<Token> tokens, Diagnostic _) = ParseExpr(code);
        return ToFlatAst(expr, tokens, code);
    }

    private static (Expr, List<Token>, Diagnostic) ParseExpr(string code)
    {
        Diagnostic diag = new();

        Lexer lexer = new(code, diag);
        List<Token> tokens = lexer.Run();

        Parser parser = new(code, tokens, diag);
        Expr expr = parser.RunExpr();

        return (expr, tokens, diag);
    }

    private static string ToFlatAst(Expr expr, List<Token> tokens, string code)
    {
        AstPrinter printer = new(tokens, code);
        string flat = printer.PrettyExpr(expr);
        return flat;
    }

    [Theory]
    [InlineData("+")]
    public void Parser_ParsesAllBinaryOps(string op)
    {
        string code = $"a {op} b";
        string expected = $"(a {op} b)";

        string parsed = ExprCodeToFlatAst(code);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData("a + b", "(a + b)")]
    public void Parser_Parses(string code, string expected)
    {
        string parsed = ExprCodeToFlatAst(code);
        Assert.Equal(expected, parsed);
    }
}
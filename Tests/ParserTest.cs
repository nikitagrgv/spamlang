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
    [InlineData(BinaryOp.Plus)]
    [InlineData(BinaryOp.Minus)]
    [InlineData(BinaryOp.Mul)]
    [InlineData(BinaryOp.Div)]
    [InlineData(BinaryOp.Rem)]
    [InlineData(BinaryOp.BitAnd)]
    [InlineData(BinaryOp.BitOr)]
    [InlineData(BinaryOp.BitXor)]
    [InlineData(BinaryOp.LogicAnd)]
    [InlineData(BinaryOp.LogicOr)]
    [InlineData(BinaryOp.Equal)]
    [InlineData(BinaryOp.NotEqual)]
    [InlineData(BinaryOp.Less)]
    [InlineData(BinaryOp.LessEqual)]
    [InlineData(BinaryOp.Greater)]
    [InlineData(BinaryOp.GreaterEqual)]
    public void Parser_ParsesAllBinaryOps(BinaryOp op)
    {
        string code = $"a {op.ToString()} b";

        (Expr expr, List<Token> tokens, Diagnostic diag) = ParseExpr(code);
        string flat = ToFlatAst(expr, tokens, code);

        Assert.False(diag.HasErrors);
        Assert.IsType<ExprBinary>(expr);
        Assert.Equal(op, ((ExprBinary)expr).Op);
        Assert.Equal($"(a {opString} b)", flat);
    }

    [Theory]
    [InlineData("+", BinaryOp.Plus)]
    [InlineData("-", BinaryOp.Minus)]
    [InlineData("*", BinaryOp.Mul)]
    [InlineData("/", BinaryOp.Div)]
    [InlineData("%", BinaryOp.Rem)]
    [InlineData("&", BinaryOp.BitAnd)]
    [InlineData("|", BinaryOp.BitOr)]
    [InlineData("^", BinaryOp.BitXor)]
    [InlineData("&&", BinaryOp.LogicAnd)]
    [InlineData("||", BinaryOp.LogicOr)]
    [InlineData("==", BinaryOp.Equal)]
    [InlineData("!=", BinaryOp.NotEqual)]
    [InlineData("<", BinaryOp.Less)]
    [InlineData("<=", BinaryOp.LessEqual)]
    [InlineData(">", BinaryOp.Greater)]
    [InlineData(">=", BinaryOp.GreaterEqual)]
    public void Parser_ParsesAllUnaryOps(string opString, UnaryOp op)
    {
        string code = $"a {opString} b";

        (Expr expr, List<Token> tokens, Diagnostic diag) = ParseExpr(code);
        string flat = ToFlatAst(expr, tokens, code);

        Assert.False(diag.HasErrors);
        Assert.IsType<ExprBinary>(expr);
        Assert.Equal(op, ((ExprBinary)expr).Op);
        Assert.Equal($"(a {opString} b)", flat);
    }

    [Theory]
    [InlineData("a + b", "(a + b)")]
    public void Parser_Parses(string code, string expected)
    {
        string parsed = ExprCodeToFlatAst(code);
        Assert.Equal(expected, parsed);
    }
}
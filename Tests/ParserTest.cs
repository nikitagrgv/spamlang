using Spamlang.DebugPrinters;
using Spamlang.Frontend;

namespace Tests;

public class ParserTest
{
    private static readonly BinaryOp[] AllBinaryOps =
    [
        BinaryOp.Plus,
        BinaryOp.Minus,
        BinaryOp.Mul,
        BinaryOp.Div,
        BinaryOp.Rem,
        BinaryOp.BitAnd,
        BinaryOp.BitOr,
        BinaryOp.BitXor,
        BinaryOp.LogicAnd,
        BinaryOp.LogicOr,
        BinaryOp.Equal,
        BinaryOp.NotEqual,
        BinaryOp.Less,
        BinaryOp.LessEqual,
        BinaryOp.Greater,
        BinaryOp.GreaterEqual,
    ];

    private static readonly UnaryOp[] AllUnaryOps =
    [
        UnaryOp.Plus,
        UnaryOp.Minus,
        UnaryOp.Not,
        UnaryOp.BitNot,
    ];

    public static TheoryData<BinaryOp> AllBinaryOpsData = MakeData(AllBinaryOps);
    public static TheoryData<UnaryOp> AllUnaryOpsData = MakeData(AllUnaryOps);

    [Fact]
    public void Lexer_CoversAllBinaryOps()
    {
        HashSet<BinaryOp> covered = AllBinaryOps.ToHashSet();
        List<BinaryOp> missing = Enum.GetValues<BinaryOp>()
            .Where(t => !covered.Contains(t))
            .ToList();
        Assert.True(missing.Count == 0, $"Missing binary ops: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Lexer_CoversAllUnaryOps()
    {
        HashSet<UnaryOp> covered = AllUnaryOps.ToHashSet();
        List<UnaryOp> missing = Enum.GetValues<UnaryOp>()
            .Where(t => !covered.Contains(t))
            .ToList();
        Assert.True(missing.Count == 0, $"Missing binary ops: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(AllBinaryOpsData))]
    public void Parser_ParsesAllBinaryOps(BinaryOp op)
    {
        string code = $"a {op.AsString()} b";

        (Expr expr, List<Token> tokens, Diagnostic diag) = ParseExpr(code);
        string flat = ToFlatAst(expr, tokens, code);

        Assert.False(diag.HasErrors);
        Assert.IsType<ExprBinary>(expr);
        Assert.Equal(op, ((ExprBinary)expr).Op);
        Assert.Equal($"(a {op.AsString()} b)", flat);
    }

    [Theory]
    [MemberData(nameof(AllUnaryOpsData))]
    public void Parser_ParsesAllUnaryOps(UnaryOp op)
    {
        string code = $"{op.AsString()}a";

        (Expr expr, List<Token> tokens, Diagnostic diag) = ParseExpr(code);
        string flat = ToFlatAst(expr, tokens, code);

        Assert.False(diag.HasErrors);
        Assert.IsType<ExprUnary>(expr);
        Assert.Equal(op, ((ExprUnary)expr).Op);
        Assert.Equal($"({op.AsString()}a)", flat);
    }

    [Theory]
    [InlineData("a + b", "(a + b)")]
    public void Parser_Parses(string code, string expected)
    {
        string parsed = ExprCodeToFlatAst(code);
        Assert.Equal(expected, parsed);
    }

    /////////////////////////////////////////////////////////////////////////

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

    private static TheoryData<T> MakeData<T>(T[] values) where T : Enum
    {
        TheoryData<T> d = new();
        foreach (T type in values)
        {
            d.Add(type);
        }

        return d;
    }
}
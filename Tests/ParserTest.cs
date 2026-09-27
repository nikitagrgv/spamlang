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
        BinaryOp.BitShiftLeft,
        BinaryOp.BitShiftRight,
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

        (Expr expr, string flat, Diagnostic diag, _) = ParseExpr(code);

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

        (Expr expr, string flat, Diagnostic diag, _) = ParseExpr(code);

        Assert.False(diag.HasErrors);
        Assert.IsType<ExprUnary>(expr);
        Assert.Equal(op, ((ExprUnary)expr).Op);
        Assert.Equal($"({op.AsString()}a)", flat);
    }

    [Fact]
    public void Parser_Cast()
    {
        string code = "a as b";

        (Expr expr, string flat, Diagnostic diag, List<Token> tokens) = ParseExpr(code);
        Assert.False(diag.HasErrors);
        Assert.IsType<ExprCast>(expr);
        Assert.Equal("a", ToFlat(((ExprCast)expr).Value, tokens, code));
        Assert.Equal("b", ToFlat(((ExprCast)expr).TargetType, tokens, code));
        Assert.Equal("(a as b)", flat);
    }

    /////////////////////////////////////////////////////////////////////////

    private class ExprAnalyzer
    {
        private readonly string _code;
        private readonly Diagnostic _diag;
        private readonly List<Token> _tokens;
        private readonly Node _result;
        private readonly string _flat;

        public string Code => _code;
        public Expr ResultExpr => (Expr)_result;
        public string Flat => _flat;
        public Diagnostic Diag => _diag;
        public bool HasErrors => _diag.HasErrors;

        public static ExprAnalyzer Parse(string code)
        {
            ExprAnalyzer analyzer = new(code);
            return analyzer;
        }

        private ExprAnalyzer(string code)
        {
            _code = code;
            _diag = new Diagnostic();

            Lexer lexer = new(_code, _diag);
            _tokens = lexer.Run();

            Parser parser = new(_code, _tokens, _diag);
            _result = parser.RunExpr();

            _flat = ToFlat(_result);
        }

        public string ToFlat(Node node)
        {
            switch (node)
            {
                case Expr expr:
                    return ToFlat(expr, _tokens, _code);
                case TypeNode typeNode:
                    return ToFlat(typeNode, _tokens, _code);
                default:
                    throw new ArgumentOutOfRangeException(nameof(node));
            }
        }

        private static string ToFlat(Expr expr, List<Token> tokens, string code)
        {
            AstPrinter printer = new(tokens, code);
            string flat = printer.PrettyExpr(expr);
            return flat;
        }

        private static string ToFlat(TypeNode node, List<Token> tokens, string code)
        {
            AstPrinter printer = new(tokens, code);
            string flat = printer.PrettyTypeNode(node);
            return flat;
        }
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
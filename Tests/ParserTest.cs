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
    public void Parser_CoversAllBinaryOps()
    {
        HashSet<BinaryOp> covered = AllBinaryOps.ToHashSet();
        List<BinaryOp> missing = Enum.GetValues<BinaryOp>()
            .Where(t => !covered.Contains(t))
            .ToList();
        Assert.True(missing.Count == 0, $"Missing binary ops: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Parser_CoversAllUnaryOps()
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

        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);

        Assert.False(analyzer.HasErrors);
        Assert.IsType<ExprBinary>(analyzer.Result);
        Assert.Equal(op, analyzer.ResultAs<ExprBinary>().Op);
        Assert.Equal($"(a {op.AsString()} b)", analyzer.Flat);
    }

    [Theory]
    [MemberData(nameof(AllUnaryOpsData))]
    public void Parser_ParsesAllUnaryOps(UnaryOp op)
    {
        string code = $"{op.AsString()}a";

        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);

        Assert.False(analyzer.HasErrors);
        Assert.IsType<ExprUnary>(analyzer.Result);
        Assert.Equal(op, analyzer.ResultAs<ExprUnary>().Op);
        Assert.Equal($"({op.AsString()}a)", analyzer.Flat);
    }

    [Fact]
    public void Parser_ParsesCast()
    {
        string code = "a as b";

        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);

        Assert.False(analyzer.HasErrors);
        Assert.IsType<ExprCast>(analyzer.Result);
        Assert.Equal("a", analyzer.ToFlat(analyzer.ResultAs<ExprCast>().Value));
        Assert.Equal("b", analyzer.ToFlat(analyzer.ResultAs<ExprCast>().TargetType));
        Assert.Equal("(a as b)", analyzer.Flat);
    }

    [Fact]
    public void Parser_ParsesCall()
    {
        string code = "f(a)";

        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);

        Assert.False(analyzer.HasErrors);
        Assert.IsType<ExprCall>(analyzer.Result);
        Assert.Equal("f", analyzer.ToFlat(analyzer.ResultAs<ExprCall>().Callee));
        Assert.Single(analyzer.ResultAs<ExprCall>().Args);
        Assert.Equal("a", analyzer.ToFlat(analyzer.ResultAs<ExprCall>().Args.First().Value));
        Assert.Equal("(f(a))", analyzer.Flat);
    }

    [Fact]
    public void Parser_ParsesNamedArgs()
    {
        string code = "f(a: b, c: d)";

        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);

        Assert.False(analyzer.HasErrors);
        Assert.IsType<ExprCall>(analyzer.Result);

        Assert.Equal(2, analyzer.ResultAs<ExprCall>().Args.Count);

        Assert.Equal("b", analyzer.ToFlat(analyzer.ResultAs<ExprCall>().Args[0].Value));
        Assert.True(analyzer.ResultAs<ExprCall>().Args[0].ArgNameToken.HasValue);
        Assert.Equal("a", analyzer.TokenToString(analyzer.ResultAs<ExprCall>().Args[0].ArgNameToken!.Value));

        Assert.Equal("d", analyzer.ToFlat(analyzer.ResultAs<ExprCall>().Args[1].Value));
        Assert.True(analyzer.ResultAs<ExprCall>().Args[1].ArgNameToken.HasValue);
        Assert.Equal("c", analyzer.TokenToString(analyzer.ResultAs<ExprCall>().Args[1].ArgNameToken!.Value));

        Assert.Equal("(f(a: b, c: d))", analyzer.Flat);
    }

    [Theory]
    [MemberData(nameof(AllBinaryOpsData))]
    public void Parser_AllBinaryOpsAreLeftAssoc(BinaryOp op)
    {
        string opStr = op.AsString();
        string code = $"a {opStr} b {opStr} c {opStr} d";

        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);
        Assert.False(analyzer.HasErrors);

        Assert.Equal($"(((a {opStr} b) {opStr} c) {opStr} d)", analyzer.Flat);
    }

    [Fact]
    public void Parser_CallsArelLeftAssoc()
    {
        string code = "a(b)(c)(d)";

        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);
        Assert.False(analyzer.HasErrors);

        Assert.Equal("(((a(b))(c))(d))", analyzer.Flat);
    }

    [Fact]
    public void Parser_AsCastsAreLeftAssoc()
    {
        string code = "a as b as c as d";

        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);
        Assert.False(analyzer.HasErrors);

        Assert.Equal("(((a as b) as c) as d)", analyzer.Flat);
    }

    [Fact]
    public void Parser_PrefixesAreRightAssoc()
    {
        string code = "-+!~a";

        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);
        Assert.False(analyzer.HasErrors);

        Assert.Equal("(-(+(!(~a))))", analyzer.Flat);
    }

    [Theory]
    [MemberData(nameof(AllUnaryOpsData))]
    public void Parser_PostfixesAreTighterThanUnary(UnaryOp op)
    {
        string opStr = op.AsString();
        string code = $"{opStr}a(b)";

        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);
        Assert.False(analyzer.HasErrors);
        Assert.Equal($"({opStr}(a(b)))", analyzer.Flat);
    }

    [Theory]
    [InlineData("-a as b", "((-a) as b)")]
    [InlineData("!a as b", "((-a) as b)")]
    [InlineData("+a as b", "((-a) as b)")]
    [InlineData("~a as b", "((-a) as b)")]
    public void Parser_PrefixesAreTighterThanAsCast(string code, string expectedFlat)
    {
        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);
        Assert.False(analyzer.HasErrors);
        Assert.Equal(expectedFlat, analyzer.Flat);
    }

    [Theory]
    [InlineData("a + b * c", "(a + (b * c))")]
    [InlineData("a * b + c", "((a * b) + c)")]
    [InlineData("-a(b)", "")]
    public void Parser_ParsesAccordingToPrecedence(string code, string expectedFlat)
    {
        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);
        Assert.False(analyzer.HasErrors);
        Assert.Equal(expectedFlat, analyzer.Flat);
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
        public Node Result => _result;
        public string Flat => _flat;
        public Diagnostic Diag => _diag;
        public bool HasErrors => _diag.HasErrors;

        public T ResultAs<T>() where T : Node => (T)_result;

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

        public string TokenToString(int tokenIndex)
        {
            return _tokens[tokenIndex].Value(_code).ToString();
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
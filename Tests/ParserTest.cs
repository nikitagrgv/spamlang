using Spamlang.DebugPrinters;
using Spamlang.Frontend;

namespace Tests;

public class ParserTest
{
    private const int BitBinaryOpPrec = 6;
    private const int CompBinaryOpPrec = 7;

    private static readonly (BinaryOp op, int prec)[] AllBinaryOpsWithPrecedence =
    [
        (BinaryOp.Mul, 4),
        (BinaryOp.Div, 4),
        (BinaryOp.Rem, 4),

        (BinaryOp.Plus, 5),
        (BinaryOp.Minus, 5),

        (BinaryOp.BitAnd, BitBinaryOpPrec),
        (BinaryOp.BitOr, BitBinaryOpPrec),
        (BinaryOp.BitXor, BitBinaryOpPrec),
        (BinaryOp.BitShiftLeft, BitBinaryOpPrec),
        (BinaryOp.BitShiftRight, BitBinaryOpPrec),

        (BinaryOp.Equal, CompBinaryOpPrec),
        (BinaryOp.NotEqual, CompBinaryOpPrec),
        (BinaryOp.Less, CompBinaryOpPrec),
        (BinaryOp.LessEqual, CompBinaryOpPrec),
        (BinaryOp.Greater, CompBinaryOpPrec),
        (BinaryOp.GreaterEqual, CompBinaryOpPrec),

        (BinaryOp.LogicAnd, 8),

        (BinaryOp.LogicOr, 9),
    ];


    private static readonly UnaryOp[] AllUnaryOps =
    [
        UnaryOp.Plus,
        UnaryOp.Minus,
        UnaryOp.Not,
        UnaryOp.BitNot,
    ];

    public static TheoryData<(BinaryOp, int)> AllBinaryOpsWithPrecedenceData = MakeData(AllBinaryOpsWithPrecedence);
    public static TheoryData<BinaryOp> AllBinaryOpsData = MakeData(AllBinaryOpsWithPrecedence.Select(t => t.op));
    public static TheoryData<UnaryOp> AllUnaryOpsData = MakeData(AllUnaryOps);

    public static TheoryData<BinaryOp> BitBinaryOpsData = MakeData(AllBinaryOpsWithPrecedence
        .Where(t => t.prec == BitBinaryOpPrec)
        .Select(t => t.op));

    public static TheoryData<BinaryOp> CompBinaryOpsData = MakeData(AllBinaryOpsWithPrecedence
        .Where(t => t.prec == CompBinaryOpPrec)
        .Select(t => t.op));

    public static TheoryData<BinaryOp> LeftAssocBinaryOpsData = MakeData(AllBinaryOpsWithPrecedence
        .Where(t => t.prec != CompBinaryOpPrec)
        .Select(t => t.op));

    [Fact]
    public void Parser_CoversAllBinaryOps()
    {
        HashSet<BinaryOp> covered = AllBinaryOpsWithPrecedence.Select(v => v.op).ToHashSet();
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
    [MemberData(nameof(LeftAssocBinaryOpsData))]
    public void Parser_AllBinaryOpsExceptComparisonsAndBitOpsAreLeftAssoc(BinaryOp op)
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
    [MemberData(nameof(AllUnaryOpsData))]
    public void Parser_UnaryAreTighterThanAsCast(UnaryOp op)
    {
        string opStr = op.AsString();
        string code = $"{opStr}a as b";

        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);
        Assert.False(analyzer.HasErrors);
        Assert.Equal($"(({opStr}a) as b)", analyzer.Flat);
    }

    [Theory]
    [MemberData(nameof(AllBinaryOpsData))]
    public void Parser_AsCastsIsTighterThanBinary(BinaryOp op)
    {
        string opStr = op.AsString();
        string code = $"a {opStr} b as c";

        ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);
        Assert.False(analyzer.HasErrors);
        Assert.Equal($"(a {opStr} (b as c))", analyzer.Flat);
    }

    [Theory]
    [MemberData(nameof(AllBinaryOpsData))]
    public void Parser_UnaryAreTighterThanBinary(BinaryOp binaryOp)
    {
        foreach (UnaryOp unaryOp in AllUnaryOps)
        {
            string op = binaryOp.AsString();
            string u = unaryOp.AsString();

            string code = $"a {op} {u}c";
            ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);
            Assert.False(analyzer.HasErrors);
            Assert.Equal($"(a {op} ({u}c))", analyzer.Flat);

            code = $"{u}a {op} c";
            analyzer = ExprAnalyzer.Parse(code);
            Assert.False(analyzer.HasErrors);
            Assert.Equal($"(({u}a) {op} c)", analyzer.Flat);
        }
    }

    [Theory]
    [MemberData(nameof(AllBinaryOpsWithPrecedenceData))]
    public void Parser_CheckPrecedenceTableOfBinaryOps((BinaryOp op, int prec) v1)
    {
        foreach ((BinaryOp op, int prec) v2 in AllBinaryOpsWithPrecedence)
        {
            if (v1.prec == CompBinaryOpPrec && v2.prec == CompBinaryOpPrec)
            {
                // Comparisons are not associative
                continue;
            }

            if (v1.prec == BitBinaryOpPrec && v2.prec == BitBinaryOpPrec && v1.op != v2.op)
            {
                // Cannot mix bit binary ops without parentheses
                continue;
            }

            string op1Str = v1.op.AsString();
            string op2Str = v2.op.AsString();

            string code = $"a {op1Str} b {op2Str} c";

            ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);
            Assert.False(analyzer.HasErrors);

            string expected;
            if (v1.prec <= v2.prec)
            {
                expected = $"((a {op1Str} b) {op2Str} c)";
            }
            else
            {
                expected = $"(a {op1Str} (b {op2Str} c))";
            }

            Assert.Equal(expected, analyzer.Flat);
        }
    }

    [Theory]
    [MemberData(nameof(CompBinaryOpsData))]
    public void Parser_ComparisonsAreNotAssociative(BinaryOp op1)
    {
        foreach (BinaryOp op2 in CompBinaryOpsData)
        {
            string op1Str = op1.AsString();
            string op2Str = op2.AsString();
            string code = $"a {op1Str} b {op2Str} c";
            ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);
            Assert.True(analyzer.HasErrors);

            // Still can combine them with parentheses
            code = $"(a {op1Str} b) {op2Str} c";
            analyzer = ExprAnalyzer.Parse(code);
            Assert.False(analyzer.HasErrors);
            Assert.Equal($"((a {op1Str} b) {op2Str} c)", analyzer.Flat);

            code = $"a {op1Str} (b {op2Str} c)";
            analyzer = ExprAnalyzer.Parse(code);
            Assert.False(analyzer.HasErrors);
            Assert.Equal($"(a {op1Str} (b {op2Str} c))", analyzer.Flat);
        }
    }

    [Theory]
    [MemberData(nameof(BitBinaryOpsData))]
    public void Parser_CannotMixBitBinaryOpsWithoutParentheses(BinaryOp op1)
    {
        foreach (BinaryOp op2 in BitBinaryOpsData)
        {
            string op1Str = op1.AsString();
            string op2Str = op2.AsString();
            string code = $"a {op1Str} b {op2Str} c";

            ExprAnalyzer analyzer = ExprAnalyzer.Parse(code);

            if (op1 == op2)
            {
                // Same bit binary ops are associative
                Assert.False(analyzer.HasErrors);
                Assert.Equal($"((a {op1Str} b) {op2Str} c)", analyzer.Flat);
            }
            else
            {
                // Can't combine different binary ops, parentheses are needed
                Assert.True(analyzer.HasErrors);
            }

            // Still can combine them with parentheses
            code = $"(a {op1Str} b) {op2Str} c";
            analyzer = ExprAnalyzer.Parse(code);
            Assert.False(analyzer.HasErrors);
            Assert.Equal($"((a {op1Str} b) {op2Str} c)", analyzer.Flat);

            code = $"a {op1Str} (b {op2Str} c)";
            analyzer = ExprAnalyzer.Parse(code);
            Assert.False(analyzer.HasErrors);
            Assert.Equal($"(a {op1Str} (b {op2Str} c))", analyzer.Flat);
        }
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

    /////////////////////////////////////////////////////////////////////////

    private static TheoryData<T> MakeData<T>(IEnumerable<T> values)
    {
        TheoryData<T> d = new();
        foreach (T type in values)
        {
            d.Add(type);
        }

        return d;
    }
}
using Spamlang.DebugPrinters;
using Spamlang.Frontend;

namespace Tests;

public class ParserTest
{
    private static readonly (BinaryOp op, int prec)[] AllBinaryOpsWithPrecedence =
    [
        (BinaryOp.Mul, Parser.MultiplicativePrec),
        (BinaryOp.Div, Parser.MultiplicativePrec),
        (BinaryOp.Rem, Parser.MultiplicativePrec),

        (BinaryOp.Plus, Parser.AddictivePrec),
        (BinaryOp.Minus, Parser.AddictivePrec),

        (BinaryOp.BitAnd, Parser.BitwisePrec),
        (BinaryOp.BitOr, Parser.BitwisePrec),
        (BinaryOp.BitXor, Parser.BitwisePrec),
        (BinaryOp.BitShiftLeft, Parser.BitwisePrec),
        (BinaryOp.BitShiftRight, Parser.BitwisePrec),

        (BinaryOp.Equal, Parser.ComparisonPrec),
        (BinaryOp.NotEqual, Parser.ComparisonPrec),
        (BinaryOp.Less, Parser.ComparisonPrec),
        (BinaryOp.LessEqual, Parser.ComparisonPrec),
        (BinaryOp.Greater, Parser.ComparisonPrec),
        (BinaryOp.GreaterEqual, Parser.ComparisonPrec),

        (BinaryOp.LogicAnd, Parser.LogicAndPrec),

        (BinaryOp.LogicOr, Parser.LogicOrPrec),
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
        .Where(t => t.prec == Parser.BitwisePrec)
        .Select(t => t.op));

    public static TheoryData<BinaryOp> CompBinaryOpsData = MakeData(AllBinaryOpsWithPrecedence
        .Where(t => t.prec == Parser.ComparisonPrec)
        .Select(t => t.op));

    public static TheoryData<BinaryOp> NonCompBinaryOpsData = MakeData(AllBinaryOpsWithPrecedence
        .Where(t => t.prec != Parser.ComparisonPrec)
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
        Assert.True(missing.Count == 0, $"Missing unary ops: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(AllBinaryOpsData))]
    public void Parser_ParsesAllBinaryOps(BinaryOp op)
    {
        string code = $"a {op.AsString()} b";

        ExprTester tester = ExprTester.Parse(code);

        Assert.False(tester.HasErrors);
        Assert.IsType<ExprBinary>(tester.Result);
        Assert.Equal(op, tester.ResultAs<ExprBinary>().Op);
        Assert.Equal($"(a {op.AsString()} b)", tester.Flat);
    }

    [Theory]
    [MemberData(nameof(AllUnaryOpsData))]
    public void Parser_ParsesAllUnaryOps(UnaryOp op)
    {
        string code = $"{op.AsString()}a";

        ExprTester tester = ExprTester.Parse(code);

        Assert.False(tester.HasErrors);
        Assert.IsType<ExprUnary>(tester.Result);
        Assert.Equal(op, tester.ResultAs<ExprUnary>().Op);
        Assert.Equal($"({op.AsString()}a)", tester.Flat);
    }

    [Fact]
    public void Parser_ParsesCast()
    {
        string code = "a as b";

        ExprTester tester = ExprTester.Parse(code);

        Assert.False(tester.HasErrors);
        Assert.IsType<ExprCast>(tester.Result);
        Assert.Equal("a", tester.ToFlat(tester.ResultAs<ExprCast>().Value));
        Assert.Equal("b", tester.ToFlat(tester.ResultAs<ExprCast>().TargetType));
        Assert.Equal("(a as b)", tester.Flat);
    }

    [Fact]
    public void Parser_ParsesCall()
    {
        string code = "f(a)";

        ExprTester tester = ExprTester.Parse(code);

        Assert.False(tester.HasErrors);
        Assert.IsType<ExprCall>(tester.Result);
        Assert.Equal("f", tester.ToFlat(tester.ResultAs<ExprCall>().Callee));
        Assert.Single(tester.ResultAs<ExprCall>().Args);
        Assert.Equal("a", tester.ToFlat(tester.ResultAs<ExprCall>().Args.First().Value));
        Assert.Equal("(f(a))", tester.Flat);
    }

    [Fact]
    public void Parser_ParsesNamedArgs()
    {
        string code = "f(a: b, c: d)";

        ExprTester tester = ExprTester.Parse(code);

        Assert.False(tester.HasErrors);
        Assert.IsType<ExprCall>(tester.Result);

        Assert.Equal(2, tester.ResultAs<ExprCall>().Args.Count);

        Assert.Equal("b", tester.ToFlat(tester.ResultAs<ExprCall>().Args[0].Value));
        Assert.True(tester.ResultAs<ExprCall>().Args[0].ArgNameToken.HasValue);
        Assert.Equal("a", tester.TokenToString(tester.ResultAs<ExprCall>().Args[0].ArgNameToken!.Value));

        Assert.Equal("d", tester.ToFlat(tester.ResultAs<ExprCall>().Args[1].Value));
        Assert.True(tester.ResultAs<ExprCall>().Args[1].ArgNameToken.HasValue);
        Assert.Equal("c", tester.TokenToString(tester.ResultAs<ExprCall>().Args[1].ArgNameToken!.Value));

        Assert.Equal("(f(a: b, c: d))", tester.Flat);
    }

    [Theory]
    [MemberData(nameof(NonCompBinaryOpsData))]
    public void Parser_AllBinaryOpsExceptComparisonsAreLeftAssoc(BinaryOp op)
    {
        string opStr = op.AsString();
        string code = $"a {opStr} b {opStr} c {opStr} d";

        ExprTester tester = ExprTester.Parse(code);
        Assert.False(tester.HasErrors);

        Assert.Equal($"(((a {opStr} b) {opStr} c) {opStr} d)", tester.Flat);
    }

    [Fact]
    public void Parser_CallsArelLeftAssoc()
    {
        string code = "a(b)(c)(d)";

        ExprTester tester = ExprTester.Parse(code);
        Assert.False(tester.HasErrors);

        Assert.Equal("(((a(b))(c))(d))", tester.Flat);
    }

    [Fact]
    public void Parser_AsCastsAreLeftAssoc()
    {
        string code = "a as b as c as d";

        ExprTester tester = ExprTester.Parse(code);
        Assert.False(tester.HasErrors);

        Assert.Equal("(((a as b) as c) as d)", tester.Flat);
    }

    [Fact]
    public void Parser_PrefixesAreRightAssoc()
    {
        string code = "-+!~a";

        ExprTester tester = ExprTester.Parse(code);
        Assert.False(tester.HasErrors);

        Assert.Equal("(-(+(!(~a))))", tester.Flat);
    }

    [Theory]
    [MemberData(nameof(AllUnaryOpsData))]
    public void Parser_PostfixesAreTighterThanUnary(UnaryOp op)
    {
        string opStr = op.AsString();
        string code = $"{opStr}a(b)";

        ExprTester tester = ExprTester.Parse(code);
        Assert.False(tester.HasErrors);
        Assert.Equal($"({opStr}(a(b)))", tester.Flat);
    }

    [Theory]
    [MemberData(nameof(AllUnaryOpsData))]
    public void Parser_UnaryAreTighterThanAsCast(UnaryOp op)
    {
        string opStr = op.AsString();
        string code = $"{opStr}a as b";

        ExprTester tester = ExprTester.Parse(code);
        Assert.False(tester.HasErrors);
        Assert.Equal($"(({opStr}a) as b)", tester.Flat);
    }

    [Theory]
    [MemberData(nameof(AllBinaryOpsData))]
    public void Parser_AsCastsIsTighterThanBinary(BinaryOp op)
    {
        string opStr = op.AsString();
        string code = $"a {opStr} b as c";

        ExprTester tester = ExprTester.Parse(code);
        Assert.False(tester.HasErrors);
        Assert.Equal($"(a {opStr} (b as c))", tester.Flat);
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
            ExprTester tester = ExprTester.Parse(code);
            Assert.False(tester.HasErrors);
            Assert.Equal($"(a {op} ({u}c))", tester.Flat);

            code = $"{u}a {op} c";
            tester = ExprTester.Parse(code);
            Assert.False(tester.HasErrors);
            Assert.Equal($"(({u}a) {op} c)", tester.Flat);
        }
    }

    [Theory]
    [MemberData(nameof(AllBinaryOpsWithPrecedenceData))]
    public void Parser_CheckPrecedenceTableOfBinaryOps((BinaryOp op, int prec) v1)
    {
        foreach ((BinaryOp op, int prec) v2 in AllBinaryOpsWithPrecedence)
        {
            if (v1.prec == Parser.ComparisonPrec && v2.prec == Parser.ComparisonPrec)
            {
                // Comparisons are not associative
                continue;
            }

            if (v1.prec == Parser.BitwisePrec && v2.prec == Parser.BitwisePrec && v1.op != v2.op)
            {
                // Cannot mix bit binary ops without parentheses
                continue;
            }

            string op1Str = v1.op.AsString();
            string op2Str = v2.op.AsString();

            string code = $"a {op1Str} b {op2Str} c";

            ExprTester tester = ExprTester.Parse(code);
            Assert.False(tester.HasErrors);

            string expected;
            if (v1.prec >= v2.prec)
            {
                expected = $"((a {op1Str} b) {op2Str} c)";
            }
            else
            {
                expected = $"(a {op1Str} (b {op2Str} c))";
            }

            Assert.Equal(expected, tester.Flat);
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
            ExprTester tester = ExprTester.Parse(code);
            Assert.True(tester.HasErrors);

            // Still can combine them with parentheses
            code = $"(a {op1Str} b) {op2Str} c";
            tester = ExprTester.Parse(code);
            Assert.False(tester.HasErrors);
            Assert.Equal($"((a {op1Str} b) {op2Str} c)", tester.Flat);

            code = $"a {op1Str} (b {op2Str} c)";
            tester = ExprTester.Parse(code);
            Assert.False(tester.HasErrors);
            Assert.Equal($"(a {op1Str} (b {op2Str} c))", tester.Flat);
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

            ExprTester tester = ExprTester.Parse(code);

            if (op1 == op2 || (IsBitShift(op1) && IsBitShift(op2)))
            {
                // Same bit binary ops are associative
                Assert.False(tester.HasErrors);
                Assert.Equal($"((a {op1Str} b) {op2Str} c)", tester.Flat);
            }
            else
            {
                // Can't combine different binary ops, parentheses are needed
                Assert.True(tester.HasErrors);
            }

            // Still can combine them with parentheses
            code = $"(a {op1Str} b) {op2Str} c";
            tester = ExprTester.Parse(code);
            Assert.False(tester.HasErrors);
            Assert.Equal($"((a {op1Str} b) {op2Str} c)", tester.Flat);

            code = $"a {op1Str} (b {op2Str} c)";
            tester = ExprTester.Parse(code);
            Assert.False(tester.HasErrors);
            Assert.Equal($"(a {op1Str} (b {op2Str} c))", tester.Flat);
        }
    }

    [Theory]
    [InlineData("a & (b | c) & d", "((a & (b | c)) & d)")]
    [InlineData("-a + ~b(c)", "((-a) + (~(b(c))))")]
    [InlineData("-a + ~b(c) * d / f == g as h | i as j || k < l",
        "((((-a) + (((~(b(c))) * d) / f)) == ((g as h) | (i as j))) || (k < l))")]
    public void Parser_ParsesComplexExpressions(string code, string expectedFlat)
    {
        ExprTester tester = ExprTester.Parse(code);
        Assert.False(tester.HasErrors);
        Assert.Equal(expectedFlat, tester.Flat);
    }

    /////////////////////////////////////////////////////////////////////////

    private class ExprTester
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

        public static ExprTester Parse(string code)
        {
            ExprTester tester = new(code);
            return tester;
        }

        private ExprTester(string code)
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
                    throw new ArgumentException($"Unsupported node type: {node.GetType().Name}", nameof(node));
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

    private bool IsBitShift(BinaryOp op)
    {
        return op == BinaryOp.BitShiftLeft || op == BinaryOp.BitShiftRight;
    }
}
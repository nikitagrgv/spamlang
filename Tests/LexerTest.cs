using System.Text;
using Spamlang;
using Spamlang.Frontend;

namespace Tests;

public class LexerTest
{
    public static readonly (string Text, TokenType Type)[] AllTokens =
    [
        ("(", TokenType.LPar),
        (")", TokenType.RPar),
        ("{", TokenType.LBrace),
        ("}", TokenType.RBrace),
        (",", TokenType.Comma),
        (":", TokenType.Colon),
        (";", TokenType.Semicolon),

        ("=", TokenType.Assign),

        ("+", TokenType.Plus),
        ("-", TokenType.Minus),
        ("*", TokenType.Star),
        ("/", TokenType.Slash),
        ("%", TokenType.Percent),

        ("==", TokenType.Equal),
        ("!=", TokenType.NotEqual),
        ("<", TokenType.Less),
        ("<=", TokenType.LessEqual),
        (">", TokenType.Greater),
        (">=", TokenType.GreaterEqual),

        ("!", TokenType.Exclamation),

        ("fn", TokenType.KeywordFunc),
        ("return", TokenType.KeywordReturn),
        ("let", TokenType.KeywordLet),
        ("as", TokenType.KeywordAs),

        ("spam", TokenType.Identifier),

        ("123", TokenType.LiteralInt),
        ("124.12", TokenType.LiteralFloat),
        ("true", TokenType.LiteralTrue),
        ("false", TokenType.LiteralFalse),

        ("->", TokenType.Arrow),
    ];

    public static TheoryData<string, TokenType> AllTokensData = MakeData();

    private static TheoryData<string, TokenType> MakeData()
    {
        TheoryData<string, TokenType> d = new();
        foreach ((string text, TokenType type) in AllTokens)
        {
            d.Add(text, type);
        }

        return d;
    }

    [Theory]
    [MemberData(nameof(AllTokensData))]
    public void Lexer_RecognizesToken(string token, TokenType expected)
    {
        string code = token;
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);
        Assert.Equal(expected, tokens[0].Type);
        Assert.Equal(token, tokens[0].Value(code));
    }

    [Theory]
    [MemberData(nameof(AllTokensData))]
    public void Lexer_AppendsEofAfterToken(string token, TokenType _)
    {
        string code = token;
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);
        Assert.NotEmpty(tokens);
        Assert.Equal(TokenType.Eof, tokens.Last().Type);
    }

    [Fact]
    public void Lexer_AppendsEofForEmptyCode()
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run("", diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);
        Assert.Single(tokens);
        Assert.Equal(TokenType.Eof, tokens.Last().Type);
    }

    [Fact]
    public void Lexer_ColumnsStartsWith1()
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run("123", diag);

        Assert.Equal(1, tokens[0].Column);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("\n123")]
    [InlineData("\n\n123")]
    [InlineData("\n\n\n\n123\n\n")]
    [InlineData(" 123", 2)]
    [InlineData("\n 123", 2)]
    [InlineData("\n\n 123", 2)]
    [InlineData("\n\n\n\n 123\n\n", 2)]
    [InlineData("  123", 3)]
    [InlineData("\n  123", 3)]
    [InlineData("\n\n  123", 3)]
    [InlineData("\n\n\n\n  123\n\n", 3)]
    public void Lexer_ColumnCountsSpaces(string code, int expectedColumn = 1)
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.Equal(expectedColumn, tokens[0].Column);
    }

    [Fact]
    public void Lexer_LinesStartsWith1()
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run("123", diag);

        Assert.Equal(1, tokens[0].Line);
    }

    [Theory]
    [InlineData("123")]
    [InlineData(" 123")]
    [InlineData("  123")]
    [InlineData("\n123", 2)]
    [InlineData("\n 123", 2)]
    [InlineData("\n  123", 2)]
    [InlineData("   \n123\n", 2)]
    [InlineData("   \n 123\n", 2)]
    [InlineData("   \n  123\n", 2)]
    [InlineData("\n \n  \n  \n\n  123\n", 6)]
    public void Lexer_LineCountsLF(string code, int expectedLine = 1)
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.Equal(expectedLine, tokens[0].Line);
    }

    [Theory]
    [InlineData("\r123")]
    [InlineData("\r 123")]
    [InlineData("\r  123")]
    [InlineData("\r\n123", 2)]
    [InlineData("\r\n 123", 2)]
    [InlineData("\r\n  123", 2)]
    [InlineData("\r\n123\n\r", 2)]
    [InlineData("\r\n 123\n\r", 2)]
    [InlineData("\r\n  123\n\r", 2)]
    [InlineData("\r\n\r\r\n\n\n\r\r\n\n\r\n123\n\r\n\r", 8)]
    public void Lexer_LineIgnoresCR(string code, int expectedLine = 1)
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.Equal(expectedLine, tokens[0].Line);
    }

    // This language is a tab hater. Sorry...
    [Theory]
    [InlineData("\t123", 0)]
    [InlineData("123\t", 3)]
    [InlineData("123\t123", 3)]
    public void Lexer_RefusesTabs(string code, int tabPos)
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.True(diag.HasErrors);
        Assert.Single(diag.Entries);

        Assert.Equal(tabPos, diag.Entries[0].Position);
        Assert.Equal(1, diag.Entries[0].Length);
        Assert.Equal(1, diag.Entries[0].Line);
        Assert.Equal(tabPos + 1, diag.Entries[0].Column);

        // Tab doesn't count as a token. All tokens must be valid
        Assert.DoesNotContain(tokens, t => t.IsInvalid());
    }

    [Fact]
    public void Lexer_TabsInCommentsAreOkay()
    {
        string code = "123 // \t\n123";
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);
        Assert.Empty(diag.Entries);

        Assert.Equal(3, tokens.Count);
        Assert.Equal(TokenType.LiteralInt, tokens[0].Type);
        Assert.Equal(TokenType.LiteralInt, tokens[1].Type);
        Assert.Equal(TokenType.Eof, tokens[2].Type);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("123")]
    [InlineData("1234567890")]
    [InlineData("1234567890123456789012345678901234567890")]
    public void Lexer_ParsesLiteralIntDecimal(string str)
    {
        string code = str;
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenType.LiteralInt, tokens[0].Type);
    }

    [Theory]
    [InlineData("00")]
    [InlineData("000")]
    [InlineData("01")]
    [InlineData("0123")]
    [InlineData("01234567")]
    [InlineData("0123456701234567")]
    [InlineData("012345670123456701234567")]
    public void Lexer_ParsesLiteralIntOctal(string str)
    {
        string code = str;
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenType.LiteralInt, tokens[0].Type);
    }

    [Theory]
    [InlineData("0x0")]
    [InlineData("0X0")]
    [InlineData("0x00")]
    [InlineData("0x01")]
    [InlineData("0x0123")]
    [InlineData("0x0123456789abcdef")]
    [InlineData("0x0123456789ABCDEF")]
    [InlineData("0X0123456789ABCDEF")]
    public void Lexer_ParsesLiteralIntHex(string str)
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(str, diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenType.LiteralInt, tokens[0].Type);
    }

    [Theory]
    [InlineData("123ab")]
    [InlineData("018")]
    [InlineData("0xfg")]
    public void Lexer_ReportsErrorForInvalidIntLiteral(string str)
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(str, diag);

        Assert.True(diag.HasErrors);
        Assert.Single(diag.Entries);
        Assert.Equal(TokenType.Invalid, tokens[0].Type);
        Assert.Equal(0, diag.Entries[0].Position);
        Assert.Equal(str.Length, diag.Entries[0].Length);
    }

    [Theory]
    [InlineData("0.0")]
    [InlineData("1.2")]
    [InlineData("1.2f")]
    [InlineData(".1")]
    [InlineData(".1f")]
    [InlineData("1.2e2")]
    [InlineData("1.2e-2")]
    [InlineData("1.2e-2f")]
    [InlineData("1.2E-2F")]
    public void Lexer_ParsesLiteralFloat(string str)
    {
        string code = str;
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenType.LiteralFloat, tokens[0].Type);
    }

    [Theory]
    [InlineData("123*", 2)]
    [InlineData("*123", 2)]
    [InlineData("-123", 2)]
    [InlineData("-spam", 2)]
    [InlineData("123*123", 3)]
    [InlineData("spam*123", 3)]
    [InlineData("spam=123", 3)]
    [InlineData("123=spam", 3)]
    public void Lexer_CanOmitWhitespacesBetweenTokens(string str, int count)
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(str, diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);
        Assert.Empty(diag.Entries);

        Assert.Equal(count + 1, tokens.Count); // + eof
        Assert.DoesNotContain(tokens, t => t.IsInvalid());
    }

    [Theory]
    [InlineData("spam")]
    [InlineData("a0x0")]
    [InlineData("sp_am")]
    [InlineData("_sp_am")]
    [InlineData("_sp_am_")]
    [InlineData("fn1")]
    [InlineData("fna")]
    [InlineData("lett")]
    [InlineData("FN")]
    [InlineData("LET")]
    [InlineData("I32")]
    [InlineData("retUrn")]
    [InlineData("_return")]
    public void Lexer_ParsesIdentifiers(string str)
    {
        string code = str;
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
    }

    [Theory]
    [InlineData("i32")]
    [InlineData("i64")]
    [InlineData("u32")]
    [InlineData("float")]
    [InlineData("double")]
    public void Lexer_PrimitiveTypesAreIdentifiers(string str)
    {
        string code = str;
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
    }

    [Theory]
    [InlineData("#abc", 1)]
    [InlineData("$abc", 1)]
    [InlineData("$#$abc", 3)]
    [InlineData("abc$#$", 3)]
    [InlineData("a$b#c$", 3)]
    [InlineData("гды123", 3)]
    [InlineData("123гды", 3)]
    public void Lexer_ReportsErrorForEachUnexpectedSymbols(string str, int numErrors)
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(str, diag);

        Assert.True(diag.HasErrors);

        Assert.Equal(numErrors, diag.Entries.Count);
    }

    [Theory]
    [InlineData("#abc", 0)]
    [InlineData("a#bc", 1)]
    [InlineData("ab#c", 2)]
    [InlineData("abc#", 3)]
    public void Lexer_ReportsPositionOfUnexpectedSymbolsCorrectly(string str, int pos)
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(str, diag);

        Assert.True(diag.HasErrors);

        Assert.Single(diag.Entries);

        Assert.Equal(pos, diag.Entries[0].Position);
        Assert.Equal(1, diag.Entries[0].Length);
    }

    [Theory]
    [InlineData("ab#cd", TokenType.Identifier, TokenType.Identifier)]
    [InlineData("ab#12", TokenType.Identifier, TokenType.LiteralInt)]
    [InlineData("12#ab", TokenType.LiteralInt, TokenType.Identifier)]
    [InlineData("12#12", TokenType.LiteralInt, TokenType.LiteralInt)]
    public void Lexer_UnexpectedSymbolSplitsCode(string str, TokenType left, TokenType right)
    {
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(str, diag);

        Assert.True(diag.HasErrors);
        Assert.Single(diag.Entries);

        Assert.Equal(4, tokens.Count);

        Assert.Equal(left, tokens[0].Type);
        Assert.Equal(TokenType.Invalid, tokens[1].Type);
        Assert.Equal(right, tokens[2].Type);
        Assert.Equal(TokenType.Eof, tokens[3].Type);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\n\r")]
    [InlineData(" \n")]
    [InlineData("\n ")]
    [InlineData(" \n \r \n\n \r")]
    // NOTE: Tab is forbidden, don't test it here
    public void Lexer_ParsesAllTokensSeparatedByWhitespace(string whitespace)
    {
        StringBuilder codeBuilder = new();
        foreach ((string text, TokenType _) in AllTokens)
        {
            codeBuilder.Append(text);
            codeBuilder.Append(whitespace);
        }

        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(codeBuilder.ToString(), diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);
        Assert.Equal(AllTokens.Length + 1, tokens.Count);

        for (int i = 0; i < AllTokens.Length; i++)
        {
            Assert.Equal(AllTokens[i].Type, tokens[i].Type);
        }

        Assert.Equal(TokenType.Eof, tokens.Last().Type);
    }

    [Fact]
    public void Lexer_ParsesSimpleProgram()
    {
        string code = "fn main() -> i32 { return 123 + 10 * x as i32; }";
        Diagnostic diag = new();
        Lexer lexer = new();
        List<Token> tokens = lexer.Run(code, diag);

        Assert.False(diag.HasErrors);
        Assert.False(diag.HasErrors);

        int cur = 0;

        void CheckNext(TokenType expected)
        {
            Assert.Equal(expected, tokens[cur].Type);
            ++cur;
        }

        CheckNext(TokenType.KeywordFunc);
        CheckNext(TokenType.Identifier);
        CheckNext(TokenType.LPar);
        CheckNext(TokenType.RPar);
        CheckNext(TokenType.Arrow);
        CheckNext(TokenType.Identifier);
        CheckNext(TokenType.LBrace);
        CheckNext(TokenType.KeywordReturn);
        CheckNext(TokenType.LiteralInt);
        CheckNext(TokenType.Plus);
        CheckNext(TokenType.LiteralInt);
        CheckNext(TokenType.Star);
        CheckNext(TokenType.Identifier);
        CheckNext(TokenType.KeywordAs);
        CheckNext(TokenType.Identifier);
        CheckNext(TokenType.Semicolon);
        CheckNext(TokenType.RBrace);
        CheckNext(TokenType.Eof);

        int expectedCount = 18;
        Assert.Equal(expectedCount, cur);
        Assert.Equal(expectedCount, tokens.Count);
    }
}
namespace Compiler;

public class Lexer
{
    private string _code = "";
    private Diagnostic? _diag;
    private bool _hasErrors = false;

    public struct Result
    {
        public List<Token> Tokens;
        public bool HasErrors;
    }

    // TODO: Make it lazy? NextToken()
    public Result Run(string code, Diagnostic diag)
    {
        _code = code;
        _diag = diag;
        _hasErrors = false;

        List<Token> tokens = [];
        int codeLen = _code.Length;
        int pos = 0;
        int line = 1;
        int column = 1;
        bool comment = false;

        while (pos < codeLen)
        {
            char c = _code[pos];

            if (c == '/' && pos + 1 < codeLen && _code[pos + 1] == '/')
            {
                comment = true;
            }

            switch (c)
            {
                case '\n':
                    comment = false;
                    line++;
                    column = 1;
                    pos++;
                    continue;
                case ' ':
                    pos++;
                    column++;
                    continue;
            }

            if (comment)
            {
                pos++;
                column++;
                continue;
            }

            if (c == '\t')
            {
                // Emit error, but don't add invalid token
                _diag.AddError("Tab characters are forbidden", pos, 1, line, column);
                pos++;
                column++;
                _hasErrors = true;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                pos++;
                column++;
                continue;
            }

            if (TryParseSingleCharToken(c) is { } singleCharToken)
            {
                Token token = new()
                {
                    Type = singleCharToken,
                    Position = pos,
                    Length = 1,
                    Line = line,
                    Column = column,
                };
                tokens.Add(token);
                pos++;
                column++;
                continue;
            }

            if (TryParseLiteralInt(_code.AsSpan(pos), out int valueLen, out bool valid))
            {
                Token token = new()
                {
                    Type = TokenType.LiteralInt,
                    Position = pos,
                    Length = valueLen,
                    Line = line,
                    Column = column,
                };

                if (!valid)
                {
                    token.Type = TokenType.Invalid;
                    _hasErrors = true;
                    _diag.AddError("Invalid integer literal", token);
                }

                tokens.Add(token);
                pos += valueLen;
                column += valueLen;
                continue;
            }

            ParseWord(_code.AsSpan(pos), out int wordLen, out valid);
            if (!valid)
            {
                Token invalidToken = new()
                {
                    Type = TokenType.Invalid,
                    Position = pos,
                    Length = wordLen,
                    Line = line,
                    Column = column,
                };
                tokens.Add(invalidToken);
                pos += wordLen;
                column += wordLen;
                _hasErrors = true;
                _diag.AddError("Invalid token", invalidToken);
                continue;
            }

            ReadOnlySpan<char> word = _code.AsSpan(pos, wordLen);
            if (TryParseKeyword(word) is { } keywordType)
            {
                Token token = new()
                {
                    Type = keywordType,
                    Position = pos,
                    Length = word.Length,
                    Line = line,
                    Column = column,
                };
                tokens.Add(token);
            }
            else
            {
                Token token = new()
                {
                    Type = TokenType.Identifier,
                    Position = pos,
                    Length = word.Length,
                    Line = line,
                    Column = column,
                };
                tokens.Add(token);
            }

            pos += word.Length;
            column += word.Length;
        }

        tokens.Add(new Token()
        {
            Type = TokenType.Eof,
            Position = pos,
            Length = 0,
            Line = line,
            Column = column
        });

        Result result = new()
        {
            Tokens = tokens,
            HasErrors = _hasErrors,
        };

        return result;
    }

    private TokenType? TryParseKeyword(ReadOnlySpan<char> word)
    {
        return word switch
        {
            "fn" => TokenType.KeywordFunc,
            "return" => TokenType.KeywordReturn,
            "let" => TokenType.KeywordLet,
            _ => null
        };
    }

    private static void ParseWord(ReadOnlySpan<char> str, out int len, out bool valid)
    {
        if (!IsWordStart(str[0]))
        {
            valid = false;
            len = 1;
            return;
        }

        int pos = 1;
        while (pos < str.Length && IsWordPart(str[pos]))
        {
            pos++;
        }

        len = pos;
        valid = true;
    }

    private static bool TryParseLiteralInt(ReadOnlySpan<char> str, out int len, out bool valid)
    {
        len = 0;
        valid = true;

        if (!char.IsAsciiDigit(str[0]))
        {
            return false;
        }

        int pos = 0;
        bool hexOrBinary = false;
        if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hexOrBinary = true;
            pos = 2;
            while (pos < str.Length && char.IsAsciiHexDigit(str[pos]))
            {
                pos++;
            }
        }
        else if (str.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
        {
            hexOrBinary = true;
            pos = 2;
            while (pos < str.Length && (str[pos] == '0' || str[pos] == '1'))
            {
                pos++;
            }
        }
        else if (str[0] == '0')
        {
            pos = 1;
            while (pos < str.Length && IsOctalDigit(str[pos]))
            {
                pos++;
            }
        }
        else
        {
            while (pos < str.Length && char.IsAsciiDigit(str[pos]))
            {
                pos++;
            }
        }

        while (pos < str.Length && (char.IsAsciiLetterOrDigit(str[pos]) || str[pos] == '_'))
        {
            valid = false;
            pos++;
        }

        len = pos;

        if (hexOrBinary && len <= 2)
        {
            valid = false;
        }

        return true;
    }

    private static bool IsOctalDigit(char c) => c >= '0' && c <= '7';

    private static bool IsWordStart(char c) => char.IsAsciiLetter(c) || c == '_';
    private static bool IsWordPart(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    private static TokenType? TryParseSingleCharToken(char c)
    {
        return c switch
        {
            '(' => TokenType.LPar,
            ')' => TokenType.RPar,
            '{' => TokenType.LBrace,
            '}' => TokenType.RBrace,
            ':' => TokenType.Colon,
            ';' => TokenType.Semicolon,
            '=' => TokenType.Assign,
            '+' => TokenType.Plus,
            '-' => TokenType.Minus,
            '*' => TokenType.Star,
            '/' => TokenType.Slash,
            '%' => TokenType.Percent,
            ',' => TokenType.Comma,
            _ => null
        };
    }
}
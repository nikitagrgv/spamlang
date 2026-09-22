namespace Spamlang.Frontend;

public class Lexer
{
    private string _code = "";
    private Diagnostic? _diag;

    public List<Token> Run(string code, Diagnostic diag)
    {
        _code = code;
        _diag = diag;

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
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                pos++;
                column++;
                continue;
            }

            if (TryParseSymbolicalToken(_code.AsSpan(pos), out int symbolTokenLen) is { } symbolToken)
            {
                Token token = new()
                {
                    Type = symbolToken,
                    Position = pos,
                    Length = symbolTokenLen,
                    Line = line,
                    Column = column,
                };
                tokens.Add(token);
                pos += symbolTokenLen;
                column += symbolTokenLen;
                continue;
            }

            if (TryParseLiteralFloat(_code.AsSpan(pos), out int valueLen, out bool valid))
            {
                Token token = new()
                {
                    Type = TokenType.LiteralFloat,
                    Position = pos,
                    Length = valueLen,
                    Line = line,
                    Column = column,
                };

                if (!valid)
                {
                    token.Type = TokenType.Invalid;
                    _diag.AddError("Invalid float literal", token);
                }

                tokens.Add(token);
                pos += valueLen;
                column += valueLen;
                continue;
            }

            if (TryParseLiteralInt(_code.AsSpan(pos), out valueLen, out valid))
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
            else if (TryParseLiteralBool(word))
            {
                Token token = new()
                {
                    Type = TokenType.LiteralBool,
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

        return tokens;
    }

    private static TokenType? TryParseKeyword(ReadOnlySpan<char> word)
    {
        return word switch
        {
            "fn" => TokenType.KeywordFunc,
            "return" => TokenType.KeywordReturn,
            "let" => TokenType.KeywordLet,
            "as" => TokenType.KeywordAs,
            _ => null,
        };
    }

    private static bool TryParseLiteralBool(ReadOnlySpan<char> word)
    {
        return word.Equals("true", StringComparison.Ordinal) ||
               word.Equals("false", StringComparison.Ordinal);
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

    // TODO: Shitty, rewrite
    private static bool TryParseLiteralFloat(ReadOnlySpan<char> str, out int len, out bool valid)
    {
        len = 0;
        valid = true;

        if (!char.IsAsciiDigit(str[0]))
        {
            return false;
        }

        // Examples:
        // 1.2
        // 1.2e2
        // 1.2e-2
        // 1.2E-2
        // 1.2E+2
        // 1e2
        // 1E2
        int pos = 1;
        while (pos < str.Length && char.IsAsciiDigit(str[pos]))
        {
            ++pos;
        }

        if (pos >= str.Length)
        {
            // Integer, not float
            return false;
        }

        bool hasExp = str[pos] != 'e' || str[pos] != 'E';
        if (str[pos] == '.')
        {
            pos++;
            bool hasDigitsAfterComma = false;
            while (pos < str.Length && char.IsAsciiDigit(str[pos]))
            {
                pos++;
                hasDigitsAfterComma = true;
            }

            if (!hasDigitsAfterComma)
            {
                valid = false;
                return true;
            }
        }
        else if (!hasExp)
        {
            valid = false;
            return true;
        }

        if (hasExp)
        {
            pos++;
            if (pos >= str.Length)
            {
                return false;
            }
        }


        while (pos < str.Length && (char.IsAsciiLetterOrDigit(str[pos]) || str[pos] == '_' || str[pos] == '.'))
        {
            valid = false;
            pos++;
        }

        len = pos;

        return true;
    }

    private static bool IsOctalDigit(char c) => c >= '0' && c <= '7';

    private static bool IsWordStart(char c) => char.IsAsciiLetter(c) || c == '_';
    private static bool IsWordPart(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    private static TokenType? TryParseSymbolicalToken(ReadOnlySpan<char> str, out int len)
    {
        len = 1;
        switch (str[0])
        {
            case '(':
                return TokenType.LPar;
            case ')':
                return TokenType.RPar;
            case '{':
                return TokenType.LBrace;
            case '}':
                return TokenType.RBrace;
            case ':':
                return TokenType.Colon;
            case ';':
                return TokenType.Semicolon;
            case '=':
                return TokenType.Assign;
            case '+':
                return TokenType.Plus;
            case '-':
                if (str.Length > 1 && str[1] == '>')
                {
                    len = 2;
                    return TokenType.Arrow;
                }

                return TokenType.Minus;
            case '*':
                return TokenType.Star;
            case '/':
                return TokenType.Slash;
            case '%':
                return TokenType.Percent;
            case ',':
                return TokenType.Comma;
            default:
                return null;
        }
    }
}
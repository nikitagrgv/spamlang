namespace Spamlang.Frontend;

public class Lexer
{
    private readonly string _code;
    private readonly Diagnostic _diag;
    private readonly List<Token> _tokens = [];
    private int _cursor;
    private int _column = 1;
    private int _line = 1;
    private bool _tabErrorEmitted = false;

    public Lexer(string code, Diagnostic diag)
    {
        _code = code;
        _diag = diag;
    }

    public List<Token> Run()
    {
        while (_cursor < _code.Length)
        {
            if (TryParseComment())
            {
                continue;
            }

            if (TryParseNewline())
            {
                continue;
            }

            if (TryParseWhitespaces())
            {
                continue;
            }

            if (TryParseSymbolicalToken())
            {
                continue;
            }

            if (TryParseNumber())
            {
                continue;
            }

            if (TryParseWord(out ReadOnlySpan<char> word))
            {
                if (TryParseKeyword(word))
                {
                    continue;
                }

                if (TryParseBool(word))
                {
                    continue;
                }

                AddToken(TokenType.Identifier, word.Length);
                continue;
            }

            _diag.AddError("Invalid token", _cursor, 1, _line, _column);
            _cursor++;
            _column++;
        }

        AddToken(TokenType.Eof, 0);
        return _tokens;
    }

    private bool TryParseComment()
    {
        if (_cursor + 1 >= _code.Length || _code[_cursor] != '/' || _code[_cursor + 1] != '/')
        {
            return false;
        }

        _cursor += 2;
        while (_cursor < _code.Length)
        {
            if (TryParseNewline())
            {
                break;
            }

            _cursor++;
        }

        return true;
    }

    private bool TryParseNewline()
    {
        if (_code[_cursor] != '\n')
        {
            return false;
        }

        _cursor++;
        _line++;
        _column = 1;
        return true;
    }

    private bool TryParseWhitespaces()
    {
        int init = _cursor;
        while (_cursor < _code.Length)
        {
            char ch = _code[_cursor];
            switch (ch)
            {
                case '\t':
                {
                    if (!_tabErrorEmitted)
                    {
                        _diag.AddError("Tab characters are forbidden", _cursor, 1, _line, _column);
                        _tabErrorEmitted = true;
                    }

                    _cursor++;
                    _column++;
                    continue;
                }
                case '\r':
                    // TODO: Add new line?
                    _cursor++;
                    continue;
                case ' ':
                    _cursor++;
                    _column++;
                    continue;
            }

            break;
        }

        return init != _cursor;
    }

    private bool TryParseSymbolicalToken()
    {
        TokenType? type = ToSymbolicalToken(_code.AsSpan(_cursor), out int len);
        if (type == null)
        {
            return false;
        }

        AddToken(type.Value, len);
        return true;
    }

    private bool TryParseWord(out ReadOnlySpan<char> word)
    {
        if (!IsWordStart(_code[_cursor]))
        {
            word = ReadOnlySpan<char>.Empty;
            return false;
        }

        int begin = _cursor;
        _cursor++;
        while (_cursor < _code.Length && IsWordPart(_code[_cursor]))
        {
            _cursor++;
        }

        int len = _cursor - begin;
        word = _code.AsSpan(begin, len);
        return true;
    }

    private bool TryParseKeyword(ReadOnlySpan<char> word)
    {
        TokenType? type = ToKeyword(word);
        if (type == null)
        {
            return false;
        }

        AddToken(type.Value, word.Length);
        return true;
    }

    private bool TryParseBool(ReadOnlySpan<char> word)
    {
        TokenType? type = ToBool(word);
        if (type == null)
        {
            return false;
        }

        AddToken(type.Value, word.Length);
        return true;
    }


    private bool TryParseNumber()
    {
        if (TryParseLiteralFloat())
        {
            return true;
        }

        // NOTE: After parse float!
        if (TryParseLiteralInt())
        {
            return true;
        }

        return false;
    }

    private bool TryParseLiteralFloat()
    {
    }

    private bool TryParseLiteralInt()
    {
        ReadOnlySpan<char> str = _code.AsSpan(_cursor);

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
            while (pos < str.Length && (str[pos] >= '0' && str[pos] <= '7'))
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

        bool valid = true;
        // Word right after the number (e.g. 123spam) - consume the word and emit error
        while (pos < str.Length && IsWordPart(str[pos]))
        {
            valid = false;
            pos++;
        }

        if (hexOrBinary && pos <= 2)
        {
            valid = false;
        }

        int len = pos;
        TokenType type = TokenType.LiteralInt;
        if (!valid)
        {
            _diag.AddError("Invalid integer literal", _cursor, len, _line, _column);
            type = TokenType.Invalid;
        }

        AddToken(type, len);
        return true;
    }

    private void AddToken(TokenType type, int len)
    {
        Token token = new()
        {
            Type = type,
            Position = _cursor,
            Length = len,
            Line = _line,
            Column = _column,
        };
        _tokens.Add(token);
        _cursor += len;
        _column += len;
    }

    private static TokenType? ToSymbolicalToken(ReadOnlySpan<char> str, out int len)
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
            case '+':
                return TokenType.Plus;
            case '*':
                return TokenType.Star;
            case '/':
                return TokenType.Slash;
            case '%':
                return TokenType.Percent;
            case ',':
                return TokenType.Comma;
            case '-':
                if (str.Length > 1 && str[1] == '>')
                {
                    len = 2;
                    return TokenType.Arrow;
                }

                return TokenType.Minus;
            case '=':
                if (str.Length > 1 && str[1] == '=')
                {
                    len = 2;
                    return TokenType.Equal;
                }
                return TokenType.Assign;
            case '>':
                if (str.Length > 1 && str[1] == '=')
                {
                    len = 2;
                    return TokenType.GreaterEqual;
                }
                return TokenType.Greater;
            case '<':
                if (str.Length > 1 && str[1] == '=')
                {
                    len = 2;
                    return TokenType.LessEqual;
                }
                return TokenType.Less;
            case '!':
                if (str.Length > 1 && str[1] == '=')
                {
                    len = 2;
                    return TokenType.NotEqual;
                }
                return TokenType.Exclamation;
            default:
                return null;
        }
    }

    private static TokenType? ToKeyword(ReadOnlySpan<char> word)
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

    private static TokenType? ToBool(ReadOnlySpan<char> word)
    {
        return word switch
        {
            "true" or "false" => TokenType.LiteralBool,
            _ => null,
        };
    }

    private static bool IsWordStart(char c) => char.IsAsciiLetter(c) || c == '_';
    private static bool IsWordPart(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
}
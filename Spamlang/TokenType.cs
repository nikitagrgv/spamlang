namespace Spamlang;

public enum TokenType
{
    Invalid,

    LPar,
    RPar,
    LBrace,
    RBrace,
    Comma,
    Colon,
    Semicolon,

    Assign,

    Plus,
    Minus,
    Star,
    Slash,
    Percent,

    KeywordFunc,
    KeywordReturn,
    KeywordLet,

    Identifier,

    LiteralInt,

    Eof,
}

public static class TokenTypeUtils
{
    extension(TokenType type)
    {
        public string PrettyName()
        {
            return type switch
            {
                TokenType.Invalid => "INVALID",

                TokenType.LPar => "(",
                TokenType.RPar => ")",
                TokenType.LBrace => "{",
                TokenType.RBrace => "}",
                TokenType.Comma => ",",
                TokenType.Colon => ":",
                TokenType.Semicolon => ";",

                TokenType.Assign => "=",
                TokenType.Plus => "+",
                TokenType.Minus => "-",
                TokenType.Star => "*",
                TokenType.Slash => "/",
                TokenType.Percent => "%",

                TokenType.KeywordFunc => "fn",
                TokenType.KeywordReturn => "return",
                TokenType.KeywordLet => "let",

                TokenType.Identifier => "$",

                TokenType.LiteralInt => "i",

                TokenType.Eof => "EOF",

                _ => throw new Exception($"Unknown token type: {type}")
            };
        }

        public string ErrorMessageName()
        {
            return type switch
            {
                TokenType.Identifier => "Identifier",
                TokenType.LiteralInt => "LiteralInt",
                _ => type.PrettyName()
            };
        }

        public bool IsLiteral
        {
            get
            {
                return type switch
                {
                    TokenType.LiteralInt => true,
                    _ => false
                };
            }
        }
    }
}
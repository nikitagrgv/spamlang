namespace Spamlang.Frontend;

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

    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,

    Ampersand,
    Pipe,

    AmpersandAmpersand,
    PipePipe,

    Caret,
    Tilde,

    ShiftLeft,
    ShiftRight,

    Exclamation,

    KeywordFunc,
    KeywordReturn,
    KeywordLet,
    KeywordAs,

    Identifier,

    LiteralInt,
    LiteralFloat,
    LiteralBool,

    Arrow,

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

                TokenType.Equal => "==",
                TokenType.NotEqual => "!=",
                TokenType.Less => "<",
                TokenType.LessEqual => "<=",
                TokenType.Greater => ">",
                TokenType.GreaterEqual => ">=",

                TokenType.Ampersand => "&",
                TokenType.Pipe => "|",

                TokenType.AmpersandAmpersand => "&&",
                TokenType.PipePipe => "||",

                TokenType.Caret => "^",
                TokenType.Tilde => "~",

                TokenType.ShiftLeft => "<<",
                TokenType.ShiftRight => ">>",

                TokenType.Exclamation => "!",

                TokenType.KeywordFunc => "fn",
                TokenType.KeywordReturn => "return",
                TokenType.KeywordLet => "let",
                TokenType.KeywordAs => "as",

                TokenType.Identifier => "$",

                TokenType.LiteralInt => "i",
                TokenType.LiteralFloat => "f",
                TokenType.LiteralBool => "b",

                TokenType.Arrow => "->",

                TokenType.Eof => "EOF",
                _ => throw new Exception($"Unknown token type: {type}"),
            };
        }

        public string ErrorMessageName()
        {
            return type switch
            {
                TokenType.Identifier => "Identifier",
                TokenType.LiteralInt => "LiteralInt",
                TokenType.LiteralFloat => "LiteralFloat",
                TokenType.LiteralBool => "LiteralBool",
                _ => type.PrettyName(),
            };
        }

        public bool IsLiteral
        {
            get
            {
                return type switch
                {
                    TokenType.LiteralInt => true,
                    TokenType.LiteralFloat => true,
                    TokenType.LiteralBool => true,
                    _ => false,
                };
            }
        }
    }
}
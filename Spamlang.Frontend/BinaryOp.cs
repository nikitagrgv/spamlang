using System.Diagnostics;

namespace Spamlang.Frontend;

public enum BinaryOp
{
    Plus,
    Minus,
    Mul,
    Div,
    Rem,

    BitAnd,
    BitOr,
    BitXor,

    LogicAnd,
    LogicOr,

    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
}

public static partial class TokenUtils
{
    public static string AsString(this BinaryOp op)
    {
        return op switch
        {
            BinaryOp.Plus => "+",
            BinaryOp.Minus => "-",
            BinaryOp.Mul => "*",
            BinaryOp.Div => "/",
            BinaryOp.Rem => "%",
            BinaryOp.BitAnd => "&",
            BinaryOp.BitOr => "|",
            BinaryOp.BitXor => "^",
            BinaryOp.LogicAnd => "&&",
            BinaryOp.LogicOr => "||",
            BinaryOp.Equal => "==",
            BinaryOp.NotEqual => "!=",
            BinaryOp.Less => "<",
            BinaryOp.LessEqual => "<=",
            BinaryOp.Greater => ">",
            BinaryOp.GreaterEqual => ">=",
            _ => throw new UnreachableException(),
        };
    }

    public static BinaryOp? ToBinaryOp(TokenType token)
    {
        return token switch
        {
            TokenType.Plus => BinaryOp.Plus,
            TokenType.Minus => BinaryOp.Minus,
            TokenType.Star => BinaryOp.Mul,
            TokenType.Slash => BinaryOp.Div,
            TokenType.Percent => BinaryOp.Rem,
            TokenType.Ampersand => BinaryOp.BitAnd,
            TokenType.Pipe => BinaryOp.BitOr,
            TokenType.Caret => BinaryOp.BitXor,
            TokenType.AmpersandAmpersand => BinaryOp.LogicAnd,
            TokenType.PipePipe => BinaryOp.LogicOr,
            TokenType.Equal => BinaryOp.Equal,
            TokenType.NotEqual => BinaryOp.NotEqual,
            TokenType.Less => BinaryOp.Less,
            TokenType.LessEqual => BinaryOp.LessEqual,
            TokenType.Greater => BinaryOp.Greater,
            TokenType.GreaterEqual => BinaryOp.GreaterEqual,
            _ => null,
        };
    }
}
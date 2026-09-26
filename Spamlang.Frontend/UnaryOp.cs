using System.Diagnostics;

namespace Spamlang.Frontend;

public enum UnaryOp
{
    Plus,
    Minus,

    Not,

    BitNot,
}

public static partial class TokenUtils
{
    public static string ToString(this UnaryOp op)
    {
        return op switch
        {
            UnaryOp.Plus => "+",
            UnaryOp.Minus => "-",
            UnaryOp.Not => "!",
            UnaryOp.BitNot => "~",
            _ => throw new UnreachableException(),
        };
    }

    public static UnaryOp ToUnaryOp(TokenType token)
    {
        return token switch
        {
            TokenType.Plus => UnaryOp.Plus,
            TokenType.Minus => UnaryOp.Minus,
            TokenType.Exclamation => UnaryOp.Not,
            TokenType.Tilde => UnaryOp.BitNot,
            _ => throw new UnreachableException(),
        };
    }
}
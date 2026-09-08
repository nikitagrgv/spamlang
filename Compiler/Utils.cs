using System.Diagnostics;

namespace Compiler;

public static class Utils
{
    public static string ToString(this UnaryOp op)
    {
        return op switch
        {
            UnaryOp.Plus => "+",
            UnaryOp.Minus => "-",
            _ => throw new UnreachableException()
        };
    }

    public static string ToString(this BinaryOp op)
    {
        return op switch
        {
            BinaryOp.Plus => "+",
            BinaryOp.Minus => "-",
            BinaryOp.Mul => "*",
            BinaryOp.Div => "/",
            BinaryOp.Rem => "%",
            _ => throw new UnreachableException()
        };
    }

    public static UnaryOp ToUnaryOp(TokenType token)
    {
        return token switch
        {
            TokenType.Plus => UnaryOp.Plus,
            TokenType.Minus => UnaryOp.Minus,
            _ => throw new UnreachableException()
        };
    }

    public static BinaryOp ToBinaryOp(TokenType token)
    {
        return token switch
        {
            TokenType.Plus => BinaryOp.Plus,
            TokenType.Minus => BinaryOp.Minus,
            TokenType.Star => BinaryOp.Mul,
            TokenType.Slash => BinaryOp.Div,
            TokenType.Percent => BinaryOp.Rem,
            _ => throw new UnreachableException()
        };
    }
}
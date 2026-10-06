using System.Diagnostics;

namespace Spamlang.Frontend;

public enum UnaryOp
{
    Plus,
    Minus,

    Not,

    BitNot,
}

public static partial class Utils
{
    public static OpFamily GetFamily(this UnaryOp op)
    {
        switch (op)
        {
            case UnaryOp.Plus:
            case UnaryOp.Minus:
                return OpFamily.Arithmetic;
            case UnaryOp.Not:
                return OpFamily.Logic;
            case UnaryOp.BitNot:
                return OpFamily.Bit;
            default: throw new UnreachableException();
        }
    }

    public static string AsString(this UnaryOp op)
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

    public static UnaryOp? ToUnaryOp(TokenType token)
    {
        return token switch
        {
            TokenType.Plus => UnaryOp.Plus,
            TokenType.Minus => UnaryOp.Minus,
            TokenType.Exclamation => UnaryOp.Not,
            TokenType.Tilde => UnaryOp.BitNot,
            _ => null,
        };
    }
}
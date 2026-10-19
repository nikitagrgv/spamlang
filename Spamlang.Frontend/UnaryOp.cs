using System.Diagnostics;

namespace Spamlang.Frontend;

public enum UnaryOp
{
    Plus,
    Minus,

    LogicNot,

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
            case UnaryOp.LogicNot:
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
            UnaryOp.LogicNot => "!",
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
            TokenType.Exclamation => UnaryOp.LogicNot,
            TokenType.Tilde => UnaryOp.BitNot,
            _ => null,
        };
    }
}
using System.Diagnostics;

namespace Compiler;

public static class Utils
{
    public static string ToSymbolString(this UnaryOp op)
    {
        return op switch
        {
            UnaryOp.Plus => "+",
            UnaryOp.Minus => "-",
            _ => throw new UnreachableException()
        };
    }

    public static string ToSymbolString(this BinaryOp op)
    {
        return op switch
        {
            BinaryOp.Plus => "+",
            BinaryOp.Minus => "-",
            BinaryOp.Mul => "*",
            BinaryOp.Div => "/",
            BinaryOp.Rem => "%",
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
        };
    }
}
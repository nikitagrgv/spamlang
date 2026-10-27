using System.Diagnostics;

namespace Spamlang.Backend;

public static class Utils
{
    public static int AlignUp(int value, int alignment)
    {
        Debug.Assert(value >= 0);
        Debug.Assert(alignment > 0);
        int aligned = value % alignment != 0 ? value : value + alignment - (value % alignment);
        Debug.Assert(IsAligned(aligned, alignment));
        return aligned;
    }

    public static bool IsAligned(int value, int alignment)
    {
        Debug.Assert(value >= 0);
        Debug.Assert(alignment > 0);
        return value % alignment == 0;
    }


    public static ulong AlignUp(ulong value, int alignment)
    {
        Debug.Assert(alignment > 0);
        ulong alignmentUlong = (ulong)alignment;
        ulong aligned = value % alignmentUlong != 0 ? value : value + alignmentUlong - (value % alignmentUlong);
        Debug.Assert(IsAligned(aligned, alignment));
        return aligned;
    }

    public static bool IsAligned(ulong value, int alignment)
    {
        Debug.Assert(alignment > 0);
        return value % (ulong)alignment == 0;
    }
}
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
}
using System.Diagnostics;

namespace Spamlang.Backend;

public static class Utils
{
    public static int AlignUp(int offset, int alignment)
    {
        Debug.Assert(offset >= 0);
        Debug.Assert(alignment > 0);
        int aligned = (offset + alignment - 1) & ~(alignment - 1);
        Debug.Assert(IsAligned(aligned, alignment));
        return aligned;
    }

    public static bool IsAligned(int offset, int alignment)
    {
        Debug.Assert(offset >= 0);
        Debug.Assert(alignment > 0);
        return offset % alignment == 0;
    }
}
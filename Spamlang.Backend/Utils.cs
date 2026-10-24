using System.Diagnostics;

namespace Spamlang.Backend;

public static class Utils
{
    public static int AlignUp(int offset, int alignment)
    {
        Debug.Assert(offset >= 0);
        return (offset + alignment - 1) & ~(alignment - 1);
    }
}
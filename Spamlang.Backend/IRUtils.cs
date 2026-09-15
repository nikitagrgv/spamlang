using Spamlang.Frontend;

namespace Spamlang.Backend;

public static class IRUtils
{
    public static SpamType ToLowerType(SpamType type)
    {
        if (type is FuncType)
        {
            return BuiltinType.Ptr;
        }

        return type;
    }

    public static FuncType ToLowerSignature(FuncType funcType, TypeRegistry typeRegistry)
    {
        // TODO: Cache!

        List<SpamType> lowerParams = new();
        lowerParams.EnsureCapacity(funcType.ParamTypes.Count);
        foreach (SpamType paramType in funcType.ParamTypes)
        {
            SpamType lower = ToLowerType(paramType);
            lowerParams.Add(lower);
        }

        SpamType lowerRetType = ToLowerType(funcType.ReturnType);
        return typeRegistry.GetFuncType(lowerRetType, lowerParams);
    }
}
namespace Spamlang;

public static class IRUtils
{
    public static Type ToLowerType(Type type)
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

        List<Type> lowerParams = new();
        lowerParams.EnsureCapacity(funcType.ParamTypes.Count);
        foreach (Type paramType in funcType.ParamTypes)
        {
            Type lower = ToLowerType(paramType);
            lowerParams.Add(lower);
        }

        Type lowerRetType = ToLowerType(funcType.ReturnType);
        return typeRegistry.GetFuncType(lowerRetType, lowerParams);
    }
}
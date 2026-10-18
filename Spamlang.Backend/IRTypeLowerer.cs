using System.Diagnostics;
using Spamlang.Frontend;

namespace Spamlang.Backend;

public class IRTypeLowerer
{
    public IRType ToLowerType(SpamType type)
    {
        if (type is FuncType)
        {
            return IRType.Ptr;
        }

        if (type.Kind == TypeKind.SignedInteger)
        {
            switch (type.Size)
            {
                case 1: return IRType.I8;
                case 2: return IRType.I16;
                case 4: return IRType.I32;
                case 8: return IRType.I64;
                default: throw new UnreachableException();
            }
        }

        return type;
    }

    public FuncType ToLowerSignature(FuncType funcType)
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
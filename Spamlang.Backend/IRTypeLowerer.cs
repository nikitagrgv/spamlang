using System.Diagnostics;
using Spamlang.Frontend;

namespace Spamlang.Backend;

public class IRTypeLowerer
{
    public IRType ToLowerType(SpamType type)
    {
        Debug.Assert(type.Kind != TypeKind.Void, "Must be handled separately, IR has no void type");

        if (type.Kind == TypeKind.AbstractNumber)
        {
            throw new InvalidHIRException("Unexpected abstract number. All abstract numbers must be transformed to the specific types");
        }

        if (type.Kind == TypeKind.Error)
        {
            throw new InvalidHIRException("Unexpected error type. IRGen works with valid HIR only");
        }

        if (type.Kind == TypeKind.Function)
        {
            return IRType.Ptr;
        }

        if (type.Kind == TypeKind.Bool)
        {
            return IRType.Bool;
        }

        if (type.Kind == TypeKind.SignedInteger || type.Kind == TypeKind.UnsignedInteger)
        {
            // Both signed and unsigned lowered to the same type, signedness is encoded in the opcode 
            switch (type.Size)
            {
                case 1: return IRType.I8;
                case 2: return IRType.I16;
                case 4: return IRType.I32;
                case 8: return IRType.I64;
                default: throw new UnexpectedTypeException(type);
            }
        }

        if (type.Kind == TypeKind.Float)
        {
            switch (type.Size)
            {
                case 4: return IRType.F32;
                case 8: return IRType.F64;
                default: throw new UnexpectedTypeException(type);
            }
        }

        throw new UnexpectedTypeException(type);
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
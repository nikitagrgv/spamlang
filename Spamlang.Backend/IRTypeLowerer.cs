using System.Diagnostics;
using Spamlang.Frontend;

namespace Spamlang.Backend;

public class IRTypeLowerer
{
    private readonly struct SignatureCacheEntry : IEquatable<SignatureCacheEntry>
    {
        public readonly IRType ReturnType;
        public readonly IReadOnlyList<IRType> ParamTypes;

        public SignatureCacheEntry(IRType returnType, IReadOnlyList<IRType> paramTypes)
        {
            ReturnType = returnType;
            ParamTypes = paramTypes;
        }

        public bool Equals(SignatureCacheEntry other)
        {
            return ReturnType == other.ReturnType && ParamTypes.SequenceEqual(other.ParamTypes);
        }

        public override bool Equals(object? obj) => obj is SignatureCacheEntry other && Equals(other);

        public override int GetHashCode()
        {
            HashCode hc = new();
            hc.Add(ReturnType);
            foreach (IRType t in ParamTypes)
            {
                hc.Add(t);
            }

            return hc.ToHashCode();
        }
    }

    private readonly Dictionary<SignatureCacheEntry, IRSignature> _signatureCache = new();

    public IRType ToLowerType(SpamType type)
    {
        switch (type.Kind)
        {
            case TypeKind.AbstractNumber: throw new InvalidHIRException("Unexpected abstract number. All abstract numbers must be transformed to the specific types");
            case TypeKind.Error: throw new InvalidHIRException("Unexpected error type. IRGen works with valid HIR only");
            case TypeKind.Void: return IRType.Void;
            case TypeKind.Function: return IRType.Ptr;
            case TypeKind.Bool: return IRType.Bool;
            case TypeKind.SignedInteger:
            case TypeKind.UnsignedInteger:
                // Both signed and unsigned lowered to the same type, signedness is encoded in opcodes
                switch (type.Size)
                {
                    case 1: return IRType.I8;
                    case 2: return IRType.I16;
                    case 4: return IRType.I32;
                    case 8: return IRType.I64;
                    default: throw new UnexpectedTypeException(type);
                }
            case TypeKind.Float:
                switch (type.Size)
                {
                    case 4: return IRType.F32;
                    case 8: return IRType.F64;
                    default: throw new UnexpectedTypeException(type);
                }
            default:
                throw new UnexpectedTypeException(type);
        }
    }

    public IRSignature ToLowerSignature(FuncType funcType)
    {
        // TODO: Reuse lists
        List<IRType> lowerParams = new();
        lowerParams.EnsureCapacity(funcType.ParamTypes.Count);
        foreach (SpamType paramType in funcType.ParamTypes)
        {
            IRType lower = ToLowerType(paramType);
            lowerParams.Add(lower);
        }

        IRType lowerRetType = ToLowerType(funcType.ReturnType);
        return GetInterned(lowerRetType, lowerParams);
    }

    private IRSignature GetInterned(IRType returnType, IReadOnlyList<IRType> paramTypes)
    {
        SignatureCacheEntry lookupSignature = new(returnType, paramTypes);
        if (_signatureCache.TryGetValue(lookupSignature, out IRSignature? type))
        {
            return type;
        }

        // copy the list, ensure immutability
        SignatureCacheEntry signature = new(returnType, [.. paramTypes]);

        type = IRSignature.CreateInterned(signature.ReturnType, signature.ParamTypes);
        _signatureCache.Add(signature, type);
        return type;
    }
}
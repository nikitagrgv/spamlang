namespace Spamlang.Frontend;

// Helper class for interning types like FuncType
public class TypeRegistry
{
    private readonly struct FuncSignature : IEquatable<FuncSignature>
    {
        public FuncSignature(SpamType returnType, IReadOnlyList<SpamType> paramTypes)
        {
            ReturnType = returnType;
            ParamTypes = paramTypes;
        }

        public readonly SpamType ReturnType;
        public readonly IReadOnlyList<SpamType> ParamTypes;

        public bool Equals(FuncSignature other)
        {
            return ReturnType == other.ReturnType && ParamTypes.SequenceEqual(other.ParamTypes);
        }

        public override bool Equals(object? obj) => obj is FuncSignature other && Equals(other);

        public override int GetHashCode()
        {
            HashCode hc = new();
            hc.Add(ReturnType);
            foreach (SpamType t in ParamTypes)
            {
                hc.Add(t);
            }

            return hc.ToHashCode();
        }
    }

    private readonly Dictionary<FuncSignature, FuncType> _funcTypes = new();

    public FuncType GetFuncType(SpamType returnType, IReadOnlyList<SpamType> paramTypes)
    {
        FuncSignature lookupSignature = new(returnType, paramTypes);
        if (_funcTypes.TryGetValue(lookupSignature, out FuncType? type))
        {
            return type;
        }

        // copy the list, ensure immutability
        FuncSignature signature = new(returnType, [.. paramTypes]);

        type = FuncType.CreateInterned(signature.ReturnType, signature.ParamTypes);
        _funcTypes.Add(signature, type);
        return type;
    }
}
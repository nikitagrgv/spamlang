namespace Compiler;

// Helper class for interning types like FuncType
public class TypeRegistry
{
    private readonly struct FuncSignature : IEquatable<FuncSignature>
    {
        public FuncSignature(Type returnType, IReadOnlyList<Type> paramTypes)
        {
            ReturnType = returnType;
            ParamTypes = paramTypes;
        }

        public readonly Type ReturnType;
        public readonly IReadOnlyList<Type> ParamTypes;

        public bool Equals(FuncSignature other)
        {
            return ReturnType == other.ReturnType && ParamTypes.SequenceEqual(other.ParamTypes);
        }

        public override bool Equals(object? obj) => obj is FuncSignature other && Equals(other);

        public override int GetHashCode()
        {
            HashCode hc = new();
            hc.Add(ReturnType);
            foreach (Type t in ParamTypes)
            {
                hc.Add(t);
            }

            return hc.ToHashCode();
        }
    }

    private readonly Dictionary<FuncSignature, FuncType> _funcTypes = new();

    public FuncType GetFuncType(Type returnType, IReadOnlyList<Type> paramTypes)
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
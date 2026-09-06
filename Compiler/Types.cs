namespace Compiler;

// NOTE: Interning is used for types like FuncType. TypeRegistry provides that. Compare by reference
public abstract class Type
{
    public abstract string Name { get; }

    public override string ToString()
    {
        return Name;
    }
}

public sealed class BuiltinType : Type
{
    public override string Name { get; }

    private BuiltinType(string name)
    {
        Name = name;
    }

    public static readonly BuiltinType I32 = new("i32");
    public static readonly BuiltinType Void = new("void");
    public static readonly BuiltinType Error = new("<error>");
}

public sealed class FuncType : Type
{
    public IReadOnlyList<Type> ParamTypes { get; }
    public Type ReturnType { get; }

    public override string Name => $"({string.Join(", ", ParamTypes.Select(t => t.Name))}): {ReturnType.Name}";

    private FuncType(Type returnType, IReadOnlyList<Type> paramTypes)
    {
        ParamTypes = paramTypes;
        ReturnType = returnType;
    }

    internal static FuncType CreateInterned(Type returnType, IReadOnlyList<Type> paramTypes)
    {
        return new FuncType(returnType, paramTypes);
    }
}
namespace Spamlang.Frontend;

// NOTE: Interning is used for types like FuncType. TypeRegistry provides that. Compare by reference
public abstract class SpamType
{
    public abstract string Name { get; }
    public abstract int Size { get; }
    public abstract int Alignment { get; }

    public override string ToString()
    {
        return Name;
    }
}

public sealed class BuiltinType : SpamType
{
    public override string Name { get; }
    public override int Size { get; }
    public override int Alignment { get; }

    private BuiltinType(string name, int size, int alignment)
    {
        Name = name;
        Size = size;
        Alignment = alignment;
    }

    public static readonly BuiltinType Void = new("void", 0, 1);

    public static readonly BuiltinType Error = new("<error>", 0, 1);

    public static readonly BuiltinType I32 = new("i32", 4, 4);

    public static readonly BuiltinType Ptr = new("ptr", 8, 8);
}

public sealed class FuncType : SpamType
{
    public IReadOnlyList<SpamType> ParamTypes { get; }
    public SpamType ReturnType { get; }

    public override int Size => 8;
    public override int Alignment => 8;

    public override string Name => $"fn({string.Join(", ", ParamTypes.Select(t => t.Name))})->{ReturnType.Name}";

    private FuncType(SpamType returnType, IReadOnlyList<SpamType> paramTypes)
    {
        ParamTypes = paramTypes;
        ReturnType = returnType;
    }

    internal static FuncType CreateInterned(SpamType returnType, IReadOnlyList<SpamType> paramTypes)
    {
        return new FuncType(returnType, paramTypes);
    }
}
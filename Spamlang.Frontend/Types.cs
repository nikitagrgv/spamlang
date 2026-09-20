namespace Spamlang.Frontend;

// NOTE: Interning is used for types like FuncType. TypeRegistry provides that. Compare by reference
public abstract class SpamType
{
    public abstract string Name { get; }
    public abstract int Size { get; }
    public abstract int Alignment { get; }
    public abstract bool IsInteger { get; }

    public override string ToString()
    {
        return Name;
    }
}

public sealed class BuiltinType : SpamType
{
    private bool _isInteger;

    public override string Name { get; }
    public override int Size { get; }
    public override int Alignment { get; }
    public override bool IsInteger => _isInteger;

    private BuiltinType(string name, int size, int alignment)
    {
        Name = name;
        Size = size;
        Alignment = alignment;
    }

    private static BuiltinType NewInteger(string name, int size, int alignment)
    {
        BuiltinType type = new(name, size, alignment);
        type._isInteger = true;
        return type;
    }

    public static readonly BuiltinType Error = new("<error>", 0, 1);

    public static readonly BuiltinType Void = new("void", 0, 1);

    public static readonly BuiltinType Ptr = new("ptr", 8, 8);

    public static readonly BuiltinType AbstractInt = NewInteger("integer", 0, 1);

    public static readonly BuiltinType I8 = NewInteger("i8", 1, 1);
    public static readonly BuiltinType I16 = NewInteger("i16", 2, 2);
    public static readonly BuiltinType I32 = NewInteger("i32", 4, 4);
    public static readonly BuiltinType I64 = NewInteger("i64", 8, 8);

    public static readonly BuiltinType U8 = NewInteger("u8", 1, 1);
    public static readonly BuiltinType U16 = NewInteger("u16", 2, 2);
    public static readonly BuiltinType U32 = NewInteger("u32", 4, 4);
    public static readonly BuiltinType U64 = NewInteger("u64", 8, 8);
}

public sealed class FuncType : SpamType
{
    public IReadOnlyList<SpamType> ParamTypes { get; }
    public SpamType ReturnType { get; }
    public override bool IsInteger => false;

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
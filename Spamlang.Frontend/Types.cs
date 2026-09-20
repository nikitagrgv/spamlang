namespace Spamlang.Frontend;

public enum TypeKind
{
    Error,
    Void,
    AbstractNumber,
    SignedInteger,
    UnsignedInteger,
    Float,
    Bool,
    Pointer,
    Function,
}

// NOTE: Interning is used for types like FuncType. TypeRegistry provides that. Compare by reference
public abstract class SpamType
{
    public abstract string Name { get; }
    public abstract int Size { get; }
    public abstract int Alignment { get; }
    public abstract TypeKind Kind { get; }

    public override string ToString()
    {
        return Name;
    }
}

public sealed class BuiltinType : SpamType
{
    private TypeKind _kind;

    public override string Name { get; }
    public override int Size { get; }
    public override int Alignment { get; }
    public override TypeKind Kind => _kind;

    private BuiltinType(TypeKind kind, string name, int size, int alignment)
    {
        _kind = kind;
        Name = name;
        Size = size;
        Alignment = alignment;
    }

    public static readonly BuiltinType Error = new(TypeKind.Error, "<error>", 0, 1);

    public static readonly BuiltinType Void = new(TypeKind.Void, "void", 0, 1);

    public static readonly BuiltinType Ptr = new(TypeKind.Pointer, "ptr", 8, 8);

    public static readonly BuiltinType AbstractNumber = new(TypeKind.AbstractNumber, "integer", 0, 1);

    public static readonly BuiltinType I8 = new(TypeKind.SignedInteger, "i8", 1, 1);
    public static readonly BuiltinType I16 = new(TypeKind.SignedInteger, "i16", 2, 2);
    public static readonly BuiltinType I32 = new(TypeKind.SignedInteger, "i32", 4, 4);
    public static readonly BuiltinType I64 = new(TypeKind.SignedInteger, "i64", 8, 8);

    public static readonly BuiltinType U8 = new(TypeKind.UnsignedInteger, "u8", 1, 1);
    public static readonly BuiltinType U16 = new(TypeKind.UnsignedInteger, "u16", 2, 2);
    public static readonly BuiltinType U32 = new(TypeKind.UnsignedInteger, "u32", 4, 4);
    public static readonly BuiltinType U64 = new(TypeKind.UnsignedInteger, "u64", 8, 8);

    public static readonly BuiltinType F32 = new(TypeKind.UnsignedInteger, "f32", 4, 4);
    public static readonly BuiltinType F64 = new(TypeKind.UnsignedInteger, "f64", 8, 8);

    public static readonly BuiltinType Bool = new(TypeKind.Bool, "bool", 1, 1);
}

public sealed class FuncType : SpamType
{
    public override string Name => $"fn({string.Join(", ", ParamTypes.Select(t => t.Name))})->{ReturnType.Name}";
    public override int Size => 8;
    public override int Alignment => 8;
    public override TypeKind Kind => TypeKind.Function;

    public IReadOnlyList<SpamType> ParamTypes { get; }
    public SpamType ReturnType { get; }

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

public static class TypesUtils
{
    public static bool IsInteger(this SpamType type)
    {
        TypeKind kind = type.Kind;
        return kind == TypeKind.SignedInteger || kind == TypeKind.UnsignedInteger;
    }

    public static bool IsFloat(this SpamType type)
    {
        return type.Kind == TypeKind.Float;
    }
}
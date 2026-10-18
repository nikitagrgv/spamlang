namespace Spamlang.Backend;

public enum IRTypeKind
{
    Int,
    Float,
    Bool,
    Ptr,
}

public sealed class IRType
{
    public string Name { get; }
    public int Size { get; }
    public int Alignment { get; } // TODO: Shouldn't be here? Move to target?
    public IRTypeKind Kind { get; }

    public static readonly IRType I8 = new IRType("i8", 1, 1, IRTypeKind.Int);
    public static readonly IRType I16 = new IRType("i16", 2, 2, IRTypeKind.Int);
    public static readonly IRType I32 = new IRType("i32", 4, 4, IRTypeKind.Int);
    public static readonly IRType I64 = new IRType("i64", 8, 8, IRTypeKind.Int);

    public static readonly IRType F32 = new IRType("f32", 4, 4, IRTypeKind.Float);
    public static readonly IRType F64 = new IRType("f64", 8, 8, IRTypeKind.Float);

    public static readonly IRType Bool = new IRType("bool", 1, 1, IRTypeKind.Bool);

    public static readonly IRType Ptr = new IRType("ptr", 8, 8, IRTypeKind.Ptr);

    private IRType(string name, int size, int alignment, IRTypeKind kind)
    {
        Name = name;
        Size = size;
        Alignment = alignment;
        Kind = kind;
    }

    public override string ToString()
    {
        return Name;
    }
}

public sealed class IRSignature
{
    public IRType? ReturnType { get; }
    public IReadOnlyList<IRType> ParamTypes { get; }

    private IRSignature(IRType? returnType, IReadOnlyList<IRType> paramTypes)
    {
        ParamTypes = paramTypes;
        ReturnType = returnType;
    }

    internal static IRSignature CreateInterned(IRType? returnType, IReadOnlyList<IRType> paramTypes)
    {
        return new IRSignature(returnType, paramTypes);
    }
}
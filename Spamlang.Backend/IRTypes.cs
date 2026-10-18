using System.Globalization;

namespace Spamlang.Backend;

public enum IRBinaryOp
{
    Add,
    Sub,
    Mul,
    SDiv,
    UDiv,
    SRem,
    URem,

    And,
    Or,
    Xor,
    ShiftLeft,
    LogicalShiftRight,
    ArithmeticShiftRight,

    FAdd,
    FSub,
    FMul,
    FDiv,
}

public enum IRUnaryOp
{
    Neg,
    Not,
    FNeg,
}

public enum IRCompareOp
{
    Equal,
    NotEqual,

    SLess,
    SLessEqual,
    SGreater,
    SGreaterEqual,

    ULess,
    ULessEqual,
    UGreater,
    UGreaterEqual,

    // NOTE: false on NaN-s
    FEqual,
    FLess,
    FLessEqual,
    FGreater,
    FGreaterEqual,

    // NOTE: true on NaN-s
    FNotEqual,
}

public enum IRCastOp
{
    Truncate,
    SignExtend,
    ZeroExtend,

    FloatExtend,
    FloatTruncate,

    UnsignedToFloat,
    SignedToFloat,

    FloatToUnsigned,
    FloatToSigned,
}

public sealed class IRModule
{
    public required List<IRFunction> Functions { get; init; }
}

public sealed class IRFunction : IRValue
{
    public required string Name { get; init; }
    public required List<IRParam> Params { get; init; }
    public required List<IRBasicBlock> BasicBlocks { get; init; }

    public required IRSignature LoweredSignature { get; init; }

    public override IRType Type => IRType.Ptr;

    public override string PrintOperand()
    {
        return $"{Type} @{Name}";
    }

    public override string PrintDefinition()
    {
        return $"{LoweredSignature} @{Name}";
    }
}

public sealed class IRBasicBlock
{
    public required string Name { get; init; }
    public required List<IRInstruction> Instructions { get; init; }

    public void Add(IRInstruction instruction)
    {
        instruction.Parent = this;
        Instructions.Add(instruction);
    }

    public IRInstruction? Terminator
    {
        get
        {
            if (Instructions.Count == 0)
            {
                return null;
            }

            IRInstruction last = Instructions[^1];
            if (!last.IsTerminator)
            {
                return null;
            }

            return last;
        }
    }
}

public abstract class IRValue
{
    public abstract IRType Type { get; }
    public int Id { get; set; } = -1;

    public virtual string PrintOperand()
    {
        return $"{Type} %{Id}";
    }

    public virtual string PrintDefinition()
    {
        return PrintOperand();
    }
}

public sealed class IRConstantInt : IRValue
{
    private readonly Int128 _canonical;
    private readonly IRType _type;

    // Canonical form: extended sign for both signed/unsigned, so 255 and -1 are the same for i8/u8
    public Int128 Canonical => _canonical;
    public override IRType Type => _type;

    private IRConstantInt(Int128 canonical, IRType type)
    {
        _canonical = canonical;
        _type = type;
    }

    public static IRConstantInt Create(Int128 value, IRType type)
    {
        Int128 canonical;
        switch (type.Kind)
        {
            case IRTypeKind.Bool:
                if (value != 0 && value != 1)
                {
                    throw new InvalidHIRException("Invalid bool value");
                }

                canonical = value;
                break;

            case IRTypeKind.Int:
            case IRTypeKind.Ptr:
                int bits = type.Size * 8;
                if (value < -(Int128.One << (bits - 1)) || value > (Int128.One << (bits - 1)))
                {
                    throw new InvalidHIRException("Integer value out of range");
                }

                int shift = 128 - bits;
                // Extend sign to the width of the type. So 255 and -1 are the same for i8/u8
                canonical = (value << shift) >> shift;
                break;

            default:
                throw new InvalidHIRException($"Cannot use type {type} for {nameof(IRConstantInt)}");
        }

        return new IRConstantInt(canonical, type);
    }

    public override string PrintOperand()
    {
        return $"{Type} {Canonical.ToString(CultureInfo.InvariantCulture)}";
    }
}

public sealed class IRConstantFloat : IRValue
{
    public required IRType FloatType { get; init; }
    public required double Value { get; init; }

    public override IRType Type => FloatType;

    public override string PrintOperand()
    {
        return $"{Type} {Value.ToString(CultureInfo.InvariantCulture)}";
    }
}

public sealed class IRParam : IRValue
{
    public required IRType ParamType { get; init; }
    public required int Index { get; init; }

    public override IRType Type => ParamType;
}

public abstract class IRInstruction : IRValue
{
    public IRBasicBlock? Parent { get; set; }
    public virtual bool IsTerminator => false;
}

public sealed class IRInstructionRet : IRInstruction
{
    public required IRValue? Value { get; init; }
    public override IRType Type => IRType.Void;
    public override bool IsTerminator => true;

    public override string PrintDefinition()
    {
        if (Value == null)
        {
            return "ret";
        }

        return $"ret {Value.PrintOperand()}";
    }
}

public sealed class IRInstructionAlloca : IRInstruction
{
    public required IRType AllocatedType { get; init; }
    public override IRType Type => IRType.Ptr;

    public override string PrintDefinition()
    {
        return $"%{Id} = alloca {AllocatedType}";
    }
}

public sealed class IRInstructionLoad : IRInstruction
{
    public required IRType LoadedType { get; init; }
    public required IRValue Address { get; init; }
    public override IRType Type => LoadedType;

    public override string PrintDefinition()
    {
        return $"%{Id} = load {LoadedType}, {Address.PrintOperand()}";
    }
}

public sealed class IRInstructionStore : IRInstruction
{
    public required IRValue Value { get; init; }
    public required IRValue Address { get; init; }
    public override IRType Type => IRType.Void;

    public override string PrintDefinition()
    {
        return $"store {Value.PrintOperand()}, {Address.PrintOperand()}";
    }
}

public sealed class IRInstructionBinary : IRInstruction
{
    public required IRBinaryOp Op { get; init; }
    public required IRValue Left { get; init; }
    public required IRValue Right { get; init; }
    public override IRType Type => Left.Type;

    public override string PrintDefinition()
    {
        return $"%{Id} = {Op} {Left.PrintOperand()}, {Right.PrintOperand()}";
    }
}

public sealed class IRInstructionUnary : IRInstruction
{
    public required IRUnaryOp Op { get; init; }
    public required IRValue Value { get; init; }
    public override IRType Type => Value.Type;

    public override string PrintDefinition()
    {
        return $"%{Id} = {Op} {Value.PrintOperand()}";
    }
}

public sealed class IRInstructionCompare : IRInstruction
{
    public required IRCompareOp Op { get; init; }
    public required IRValue Left { get; init; }
    public required IRValue Right { get; init; }
    public override IRType Type => IRType.Bool;

    public override string PrintDefinition()
    {
        return $"%{Id} = {Op} {Left.PrintOperand()}, {Right.PrintOperand()}";
    }
}

public sealed class IRInstructionCast : IRInstruction
{
    public required IRCastOp Op { get; init; }
    public required IRValue Value { get; init; }
    public required IRType CastTo { get; init; }
    public override IRType Type => CastTo;

    public override string PrintDefinition()
    {
        return $"%{Id} = cast {Op} {CastTo}, {Value.PrintOperand()}";
    }
}

public sealed class IRInstructionCall : IRInstruction
{
    public required IRValue Callee { get; init; }
    public required List<IRValue> Args { get; init; }
    public required IRSignature LoweredSignature { get; init; }

    public override IRType Type => LoweredSignature.ReturnType;

    public override string PrintDefinition()
    {
        string ret = "";
        if (Type != IRType.Void)
        {
            ret += $"%{Id} = ";
        }

        string name;
        if (Callee is IRFunction func)
        {
            name = $"@{func.Name}";
        }
        else
        {
            name = $"{Callee.PrintOperand()}";
        }

        ret += $"call {Type} {name}({string.Join(", ", Args.Select(a => a.PrintOperand()))})";
        return ret;
    }
}
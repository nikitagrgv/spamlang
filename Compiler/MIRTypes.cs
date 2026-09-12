namespace Compiler;

public enum MOpcode
{
    Mov,
    Add,
    Sub,

    Imul,

    Dec,
    Inc,

    Lea,


    Push,
    Pop,

    Call,
    Ret,
}

public static class MOpcodes
{
    public static string AsmName(this MOpcode opcode)
    {
        return opcode switch
        {
            MOpcode.Mov => "mov",
            MOpcode.Add => "add",
            MOpcode.Sub => "sub",
            MOpcode.Imul => "imul",
            MOpcode.Dec => "dec",
            MOpcode.Inc => "inc",
            MOpcode.Lea => "lea",
            MOpcode.Push => "push",
            MOpcode.Pop => "pop",
            MOpcode.Call => "call",
            MOpcode.Ret => "ret",
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }
}

public abstract class MOperand;

public sealed class MOpReg : MOperand
{
    public required Reg Reg { get; init; }
    public required int Size { get; init; }

    public string Name => Reg.GetName(Size);
}

// TODO: Float/double
public sealed class MOpImm : MOperand
{
    public required Int128 Value { get; init; }
}

public sealed class MOpMem : MOperand
{
    public required Reg Base { get; init; }
    public required int Offset { get; init; }
    public required int Size { get; init; }
}

public sealed class MOpLabel : MOperand
{
    public required string Label { get; init; }
}

public sealed class MInstr
{
    public required MOpcode Op { get; init; }
    public MOperand? Left { get; init; } = null;
    public MOperand? Right { get; init; } = null;
    public string? Comment { get; init; } = null;
}

public sealed class MModule
{
    public required List<MFunction> Functions { get; init; }
}

public sealed class MFunction
{
    public required string Name { get; init; }
    public required List<MBasicBlock> BasicBlocks { get; init; }
}

public sealed class MBasicBlock
{
    public required string? Name { get; init; }
    public required List<MInstr> Instructions { get; init; }
}
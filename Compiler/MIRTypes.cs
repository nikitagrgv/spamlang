namespace Compiler;

public enum MOpcode
{
    Mov,
    Add,
    Sub,

    Dec,
    Inc,

    Lea,


    Push,
    Pop,

    Call,
    Ret,
}

public abstract class MOperand;

public sealed class MOpReg : MOperand
{
    public required Reg Reg { get; init; }
    public required int Size { get; init; }
}

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
    public required MOperand? Left { get; init; }
    public required MOperand? Right { get; init; }
    public required string? Comment { get; init; }
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
    public required string Name { get; init; }
    public required List<MInstr> Instructions { get; init; }
}
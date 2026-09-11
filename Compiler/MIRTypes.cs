using System.Xml;

namespace Compiler;

enum MOpcode
{
    Mov,
    Add,
    Sub,
    Lea,
    Push,
    Pop,
    Call,
    Ret,
    Dec,
    Inc,
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
}
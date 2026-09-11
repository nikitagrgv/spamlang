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

public static class MOpcodes
{
    public static string AsmName(this MOpcode opcode)
    {
        return opcode switch
        {
            MOpcode.Mov => "mov",
            MOpcode.Add => "add",
            MOpcode.Sub => "sub",
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

    // TODO: Shittttt
    public static readonly MOpReg Rax = new() { Reg = Reg.Rax, Size = 8 };
    public static readonly MOpReg Rcx = new() { Reg = Reg.Rcx, Size = 8 };
    public static readonly MOpReg Rdx = new() { Reg = Reg.Rdx, Size = 8 };
    public static readonly MOpReg Rbx = new() { Reg = Reg.Rbx, Size = 8 };
    public static readonly MOpReg Rsp = new() { Reg = Reg.Rsp, Size = 8 };
    public static readonly MOpReg Rbp = new() { Reg = Reg.Rbp, Size = 8 };
    public static readonly MOpReg Rsi = new() { Reg = Reg.Rsi, Size = 8 };
    public static readonly MOpReg Rdi = new() { Reg = Reg.Rdi, Size = 8 };
    public static readonly MOpReg R8 = new() { Reg = Reg.R8, Size = 8 };
    public static readonly MOpReg R9 = new() { Reg = Reg.R9, Size = 8 };
    public static readonly MOpReg R10 = new() { Reg = Reg.R10, Size = 8 };
    public static readonly MOpReg R11 = new() { Reg = Reg.R11, Size = 8 };
    public static readonly MOpReg R12 = new() { Reg = Reg.R12, Size = 8 };
    public static readonly MOpReg R13 = new() { Reg = Reg.R13, Size = 8 };
    public static readonly MOpReg R14 = new() { Reg = Reg.R14, Size = 8 };
    public static readonly MOpReg R15 = new() { Reg = Reg.R15, Size = 8 };

    public static readonly MOpReg Eax = new() { Reg = Reg.Rax, Size = 4 };
    public static readonly MOpReg Ecx = new() { Reg = Reg.Rcx, Size = 4 };
    public static readonly MOpReg Edx = new() { Reg = Reg.Rdx, Size = 4 };
    public static readonly MOpReg Ebx = new() { Reg = Reg.Rbx, Size = 4 };
    public static readonly MOpReg Esp = new() { Reg = Reg.Rsp, Size = 4 };
    public static readonly MOpReg Ebp = new() { Reg = Reg.Rbp, Size = 4 };
    public static readonly MOpReg Esi = new() { Reg = Reg.Rsi, Size = 4 };
    public static readonly MOpReg Edi = new() { Reg = Reg.Rdi, Size = 4 };
    public static readonly MOpReg R8d = new() { Reg = Reg.R8, Size = 4 };
    public static readonly MOpReg R9d = new() { Reg = Reg.R9, Size = 4 };
    public static readonly MOpReg R10d = new() { Reg = Reg.R10, Size = 4 };
    public static readonly MOpReg R11d = new() { Reg = Reg.R11, Size = 4 };
    public static readonly MOpReg R12d = new() { Reg = Reg.R12, Size = 4 };
    public static readonly MOpReg R13d = new() { Reg = Reg.R13, Size = 4 };
    public static readonly MOpReg R14d = new() { Reg = Reg.R14, Size = 4 };
    public static readonly MOpReg R15d = new() { Reg = Reg.R15, Size = 4 };
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
    public required string Name { get; init; }
    public required List<MInstr> Instructions { get; init; }
}
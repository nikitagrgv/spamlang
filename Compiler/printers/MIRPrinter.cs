namespace Compiler.printers;

public class MIRPrinter
{
    private readonly TextWriter _writer;

    private MIRPrinter(TextWriter writer)
    {
        _writer = writer;
    }

    public static void Print(MModule module, TextWriter writer)
    {
        MIRPrinter printer = new(writer);
        printer.Print(module);
    }

    private void Print(MModule module)
    {
        foreach (MFunction func in module.Functions)
        {
            Print(func);
        }
    }

    private void Print(MFunction func)
    {
        _writer.WriteLine("# -------------");
        _writer.WriteLine($"{func.Name}:");
        foreach (MBasicBlock bb in func.BasicBlocks)
        {
            Print(bb);
        }

        _writer.WriteLine();
    }

    private void Print(MBasicBlock bb)
    {
        if (!string.IsNullOrEmpty(bb.Name))
        {
            _writer.WriteLine($"{bb.Name}:");
        }

        foreach (MInstr instr in bb.Instructions)
        {
            Print(instr);
        }
    }

    private void Print(MInstr instr)
    {
        _writer.Write($"    {instr.Op.AsmName(),-5}");
        if (instr.Left != null)
        {
            Print(instr.Left);
        }

        if (instr.Right != null)
        {
            _writer.Write(", ");
            Print(instr.Right);
        }

        if (!string.IsNullOrEmpty(instr.Comment))
        {
            _writer.Write($"              # {instr.Comment}");
        }

        _writer.WriteLine();
    }

    private void Print(MOperand op)
    {
        switch (op)
        {
            case MOpImm mOpImm:
                _writer.Write(mOpImm.Value);
                break;
            case MOpLabel mOpLabel:
                _writer.Write(mOpLabel.Label);
                break;
            case MOpReg mOpReg:
                _writer.Write(mOpReg.Reg.GetName(mOpReg.Size));
                break;
            case MOpMem mOpMem:
                _writer.Write($"[{mOpMem.Base.GetName(mOpMem.Size)}{mOpMem.Offset:+#;-#;+0}]");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(op));
        }
    }
}
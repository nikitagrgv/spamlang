using System.Text;

namespace Compiler.printers;

public class MIRPrinter
{
    private readonly TextWriter _writer;
    private int _curPos = 0;

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
        _curPos = 0;
        foreach (MFunction func in module.Functions)
        {
            Print(func);
        }
    }

    private void Print(MFunction func)
    {
        WriteLine("# -------------");
        WriteLine($"{func.Name}:");
        foreach (MBasicBlock bb in func.BasicBlocks)
        {
            Print(bb);
        }

        WriteLine();
    }

    private void Print(MBasicBlock bb)
    {
        if (!string.IsNullOrEmpty(bb.Name))
        {
            WriteLine($"{bb.Name}:");
        }

        foreach (MInstr instr in bb.Instructions)
        {
            Print(instr);
        }
    }

    private void Print(MInstr instr)
    {
        bool hasComment = !string.IsNullOrEmpty(instr.Comment);
        if (hasComment)
        {
            Console.WriteLine();
        }

        Write($"    {instr.Op.AsmName(),-5}");
        if (instr.Left != null)
        {
            Print(instr.Left);
        }

        if (instr.Right != null)
        {
            Write(", ");
            Print(instr.Right);
        }

        if (hasComment)
        {
            Write($"              # {instr.Comment}");
        }

        WriteLine();
    }

    private void Print(MOperand op)
    {
        switch (op)
        {
            case MOpImm mOpImm:
                Write(mOpImm.Value.ToString());
                break;
            case MOpLabel mOpLabel:
                Write(mOpLabel.Label);
                break;
            case MOpReg mOpReg:
                Write(mOpReg.Reg.GetName(mOpReg.Size));
                break;
            case MOpMem mOpMem:
                string? prefix = null;
                if (mOpMem.Size != null && mOpMem.Size.Value != 8)
                {
                    prefix = Regs.MemPrefix(mOpMem.Size.Value) + " ";
                }

                string name = mOpMem.Base.GetName(8);
                int offset = mOpMem.Offset;
                Write($"{prefix}[{name}{offset:+#;-#;+0}]");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(op));
        }
    }

    private void WriteLine()
    {
        _curPos = 0;
        _writer.WriteLine();
    }

    private void WriteLine(string str)
    {
        _curPos = 0;
        _writer.WriteLine(str);
    }

    private void Write(string str)
    {
        _writer.Write(str);
        _curPos += str.Length;
    }
}
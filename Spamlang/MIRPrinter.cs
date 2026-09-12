namespace Spamlang;

public class MIRPrinter
{
    private const int CommentPadding = 40;
    private static readonly char[] Pad = Enumerable.Repeat(' ', CommentPadding).ToArray();
    private readonly TextWriter _writer;
    private int _curColumn = 0;

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
        _curColumn = 0;
        PrintHeader(module);
        foreach (MFunction func in module.Functions)
        {
            Print(func);
        }
    }

    private void PrintHeader(MModule module)
    {
        WriteLine("    .intel_syntax noprefix");
        WriteLine("    .text");
        if (module.Exports.Count > 0)
        {
            Write("    .globl ");
            for (int i = 0; i < module.Exports.Count; i++)
            {
                if (i != 0)
                {
                    Write(", ");
                }

                Write(module.Exports[i]);
            }

            WriteLine();
        }

        WriteLine();
        WriteLine();
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

        for (int i = 0; i < bb.Instructions.Count; i++)
        {
            MInstr instr = bb.Instructions[i];

            bool needNewline = i != 0 && !string.IsNullOrEmpty(instr.Comment);
            if (needNewline)
            {
                WriteLine();
            }

            Print(instr);
        }
    }

    private void Print(MInstr instr)
    {
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

        if (!string.IsNullOrEmpty(instr.Comment))
        {
            int numToPad = CommentPadding - _curColumn;
            if (numToPad <= 0)
            {
                numToPad = 2;
            }

            Write(Pad.AsSpan(0, numToPad));
            Write($"# {instr.Comment}");
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
                string name = mOpMem.Base.GetName(8);
                int? offset = mOpMem.Offset;
                string? prefix = null;
                if (mOpMem.Size != null && mOpMem.Size.Value != 8)
                {
                    prefix = Regs.MemPrefix(mOpMem.Size.Value) + " ";
                }

                if (prefix != null)
                {
                    Write(prefix);
                }

                Write("[");

                Write(name);

                if (offset != null)
                {
                    Write($"{offset:+#;-#;+0}");
                }

                Write("]");

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(op));
        }
    }

    private void WriteLine()
    {
        _writer.WriteLine();
        _curColumn = 0;
    }

    private void WriteLine(ReadOnlySpan<char> str)
    {
        _writer.WriteLine(str);
        _curColumn = 0;
    }

    private void Write(ReadOnlySpan<char> str)
    {
        _writer.Write(str);
        _curColumn += str.Length;
    }
}
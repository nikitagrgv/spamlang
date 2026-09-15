namespace Spamlang.Backend;

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

    private void Print(MOperand operand)
    {
        switch (operand)
        {
            case MOpImm op:
                Write(op.Value.ToString());
                break;
            case MOpLabel op:
                Write(op.Label);
                break;
            case MOpReg op:
                Write(op.Reg.GetName(op.Size));
                break;
            case MOpMem op:
            {
                if (op.Size != null && op.Size.Value != 8)
                {
                    string prefix = Regs.MemPrefix(op.Size.Value) + " ";
                    Write(prefix);
                }

                Write("[");

                Write(op.Base.GetName());

                int? offset = op.Offset;
                if (offset != null)
                {
                    Write($"{offset:+#;-#;+0}");
                }

                Write("]");

                break;
            }
            case MOpMemLabel op:
            {
                if (op.Size != null && op.Size.Value != 8)
                {
                    string prefix = Regs.MemPrefix(op.Size.Value) + " ";
                    Write(prefix);
                }

                string regName = op.Base.GetName();
                Write($"[{regName} + {op.Label}]");

                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(operand));
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
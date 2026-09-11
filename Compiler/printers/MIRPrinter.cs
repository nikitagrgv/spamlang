namespace Compiler.printers;

public class MIRPrinter
{
    public static void Print(MModule module)
    {
        foreach (MFunction func in module.Functions)
        {
            Print(func);
        }
    }

    private static void Print(MFunction func)
    {
        Console.WriteLine("# -------------------------------------");
        Console.WriteLine($"{func.Name}:");
        foreach (MBasicBlock bb in func.BasicBlocks)
        {
            Print(bb);
        }

        Console.WriteLine();
    }

    private static void Print(MBasicBlock bb)
    {
        if (!string.IsNullOrEmpty(bb.Name))
        {
            Console.WriteLine($"{bb.Name}:");
        }

        foreach (MInstr instr in bb.Instructions)
        {
            Print(instr);
        }
    }

    private static void Print(MInstr instr)
    {
        Console.Write($"    {instr.Op.AsmName(),-5}");
        if (instr.Left != null)
        {
            Print(instr.Left);
        }

        if (instr.Right != null)
        {
            Console.Write(", ");
            Print(instr.Right);
        }

        if (!string.IsNullOrEmpty(instr.Comment))
        {
            Console.Write($"              # {instr.Comment}");
        }

        Console.WriteLine();
    }

    private static void Print(MOperand op)
    {
        switch (op)
        {
            case MOpImm mOpImm:
                Console.Write(mOpImm.Value);
                break;
            case MOpLabel mOpLabel:
                Console.Write(mOpLabel.Label);
                break;
            case MOpReg mOpReg:
                Console.Write(mOpReg.Reg.GetName(mOpReg.Size));
                break;
            case MOpMem mOpMem:
                Console.Write($"[{mOpMem.Base.GetName(mOpMem.Size)}{mOpMem.Offset:+#,-#,0}]");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(op));
        }
    }
}
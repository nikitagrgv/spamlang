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
    }

    private static void Print(MBasicBlock bb)
    {
        if (string.IsNullOrEmpty(bb.Name))
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
    }
}
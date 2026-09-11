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
    }
}
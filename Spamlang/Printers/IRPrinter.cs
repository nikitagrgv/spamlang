namespace Spamlang.Printers;

public class IRPrinter
{
    public static void Print(IRModule module)
    {
        foreach (IRFunction func in module.Functions)
        {
            PrintIR(func);
            Console.WriteLine();
        }
    }

    private static void PrintIR(IRFunction func)
    {
        Console.Write($"fn @{func.Name}(");
        for (int i = 0; i < func.Params.Count; i++)
        {
            IRParam param = func.Params[i];
            if (i != 0)
            {
                Console.Write(", ");
            }

            Console.Write($"{param.Type} %{param.Id}");
        }

        Console.Write($") -> {func.Signature.ReturnType}");
        Console.WriteLine();
        Console.WriteLine("{");
        foreach (IRBasicBlock bb in func.BasicBlocks)
        {
            PrintIR(bb);
        }

        Console.WriteLine("}");
    }

    private static void PrintIR(IRBasicBlock bb)
    {
        Console.WriteLine($"{bb.Name}:");
        foreach (IRInstruction inst in bb.Instructions)
        {
            Console.WriteLine($"  {inst.PrintDefinition()}");
        }
    }
}
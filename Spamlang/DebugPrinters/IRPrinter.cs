using Spamlang.Backend;

namespace Spamlang.DebugPrinters;

public class IRPrinter
{
    private readonly TextWriter _writer;

    public static void Print(IRModule module, TextWriter writer)
    {
        IRPrinter printer = new(writer);
        printer.Print(module);
    }

    private IRPrinter(TextWriter writer)
    {
        _writer = writer;
    }

    private void Print(IRModule module)
    {
        foreach (IRFunction func in module.Functions)
        {
            PrintFunc(func);
            Console.WriteLine();
        }
    }

    private void PrintFunc(IRFunction func)
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

        Console.Write($") -> {func.LoweredSignature.ReturnType}");
        Console.WriteLine();
        Console.WriteLine("{");
        foreach (IRBasicBlock bb in func.BasicBlocks)
        {
            PrintBlock(bb);
        }

        Console.WriteLine("}");
    }

    private void PrintBlock(IRBasicBlock bb)
    {
        Console.WriteLine($"{bb.Name}:");
        foreach (IRInstruction inst in bb.Instructions)
        {
            Console.WriteLine($"  {inst.PrintDefinition()}");
        }
    }
}
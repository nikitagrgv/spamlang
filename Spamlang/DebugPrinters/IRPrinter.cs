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
            _writer.WriteLine();
        }
    }

    private void PrintFunc(IRFunction func)
    {
        _writer.Write($"fn @{func.Name}(");
        for (int i = 0; i < func.Params.Count; i++)
        {
            IRParam param = func.Params[i];
            if (i != 0)
            {
                _writer.Write(", ");
            }

            _writer.Write($"{param.Type} %{param.Id}");
        }

        _writer.Write($") -> {func.LoweredSignature.ReturnType}");
        _writer.WriteLine();
        _writer.WriteLine("{");
        foreach (IRBasicBlock bb in func.BasicBlocks)
        {
            PrintBlock(bb);
        }

        _writer.WriteLine("}");
    }

    private void PrintBlock(IRBasicBlock bb)
    {
        _writer.WriteLine($"{bb.Name}:");
        foreach (IRInstruction inst in bb.Instructions)
        {
            _writer.WriteLine($"  {inst.PrintDefinition()}");
        }
    }
}
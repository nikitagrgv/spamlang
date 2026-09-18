using Spamlang.Frontend;

namespace Spamlang.DebugPrinters;

public class HIRPrinter
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly string _code;

    public static void Print(HIRCompilationUnit unit, IReadOnlyList<Token> tokens, string code)
    {
        HIRPrinter printer = new(tokens, code);
        printer.PrintHIR(unit);
    }

    private HIRPrinter(IReadOnlyList<Token> tokens, string code)
    {
        _code = code;
        _tokens = tokens;
    }

    private void PrintHIR(HIRCompilationUnit unit)
    {
        PrintHIR(0, unit);
    }

    private void PrintHIR(int depth, HIRNode node, string prefix = "")
    {
    }

    private static string MakeIndent(int depth)
    {
        string indent = "";
        for (int i = 0; i < depth; i++)
        {
            indent += " |   ";
        }

        return indent;
    }
}
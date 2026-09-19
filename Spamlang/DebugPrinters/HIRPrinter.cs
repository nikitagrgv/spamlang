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

    // TODO: Pretty expr
    private void PrintHIR(int depth, HIRNode node, string prefix = "")
    {
        string fullPrefix = MakeIndent(depth);
        if (prefix != "")
        {
            fullPrefix += prefix + ": ";
        }

        fullPrefix += node.GetType().Name;

        if (node.IsSynthesized)
        {
            fullPrefix += " [SYNTH]";
        }

        if (node is HIRExpr expr)
        {
            fullPrefix += $" | Type = {expr.Type}";
        }

        switch (node)
        {
            case HIRCompilationUnit n:
                Console.WriteLine($"{fullPrefix}");
                PrintChildren(depth + 1, n.FuncDecls);
                break;
            case HIRBlock n:
                Console.WriteLine($"{fullPrefix}");
                PrintSymbols(depth + 1, n.Variables, "Variable");
                PrintChildren(depth + 1, n.Stmts);
                break;
            case HIRExprLocalRef n:
                Console.WriteLine($"{fullPrefix}");
                PrintSymbol(depth + 1, n.Symbol);
                break;
            case HIRExprFuncRef n:
                Console.WriteLine($"{fullPrefix}");
                PrintSymbol(depth + 1, n.Symbol);
                break;
            case HIRFuncDecl n:
                Console.WriteLine($"{fullPrefix}");
                PrintSymbol(depth + 1, n.Symbol);
                PrintSymbols(depth + 1, n.Locals, "Local");
                PrintHIR(depth + 1, n.Body, "Body");
                break;
            case HIRStmtLet n:
                Console.WriteLine($"{fullPrefix}");
                PrintSymbol(depth + 1, n.VariableSymbol, "Variable");
                PrintHIR(depth + 1, n.Init, "Init");
                break;
            case HIRStmtReturn n:
                Console.WriteLine($"{fullPrefix}");
                if (n.Value != null)
                {
                    PrintHIR(depth + 1, n.Value, "Value");
                }

                break;
            case HIRStmtAssign n:
                Console.WriteLine($"{fullPrefix}");
                PrintHIR(depth + 1, n.Target, "Target");
                PrintHIR(depth + 1, n.Value, "Value");
                break;
            case HIRStmtExpr n:
                Console.WriteLine($"{fullPrefix}");
                PrintHIR(depth + 1, n.Expr, "Expr");
                break;
            case HIRExprBinary n:
                Console.WriteLine($"{fullPrefix} | Op = {n.Op}");
                PrintHIR(depth + 1, n.Left, "Left");
                PrintHIR(depth + 1, n.Right, "Right");
                break;
            case HIRExprUnary n:
                Console.WriteLine($"{fullPrefix} | Op = {n.Op}");
                PrintHIR(depth + 1, n.Operand, "Operand");
                break;
            case HIRExprZeroInit n:
                Console.WriteLine($"{fullPrefix}");
                break;
            case HIRExprCall n:
                Console.WriteLine($"{fullPrefix}");
                PrintHIR(depth + 1, n.Callee, "Callee");
                PrintChildren(depth + 1, n.Args);
                break;
            case HIRExprCast n:
                Console.WriteLine($"{fullPrefix}");
                PrintHIR(depth + 1, n.Value, "Value");
                break;
            case HIRExprError n:
                Console.WriteLine($"{fullPrefix}");
                PrintChildren(depth + 1, n.Children);
                break;
            case HIRCallArg n:
                Console.WriteLine($"{fullPrefix} | ParameterIndex = {n.ParameterIndex}");
                PrintHIR(depth + 1, n.Value, "Value");
                break;
            case HIRExprIntConst n:
                Console.WriteLine($"{fullPrefix} | Value = {n.Value}");
                break;
            case HIRExprLoad n:
                Console.WriteLine($"{fullPrefix}");
                PrintHIR(depth + 1, n.Address, "Address");
                break;
            default: throw new Exception("Unknown node type: " + node.GetType().Name);
        }
    }

    private void PrintSymbol(int depth, Symbol sym, string name = "")
    {
        string prefix = MakeIndent(depth);
        if (name != "")
        {
            prefix += name;
        }
        else
        {
            prefix += "Symbol";
        }

        Console.WriteLine($"{prefix}: {sym.Name} ({sym.SymbolKindName}) | Type = {sym.Type}");
    }

    private void PrintSymbols(int depth, IReadOnlyList<Symbol> symbols, string name = "")
    {
        foreach (Symbol sym in symbols)
        {
            PrintSymbol(depth, sym, name);
        }
    }

    private void PrintChildren(int depth, IReadOnlyList<HIRNode> nodes)
    {
        foreach (HIRNode node in nodes)
        {
            PrintHIR(depth, node);
        }
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
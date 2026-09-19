using System.Text;
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

        switch (node)
        {
            case HIRCompilationUnit n:
                Console.WriteLine($"{fullPrefix}");
                PrintChildrenAst(depth + 1, n.FuncDecls);
                break;
            case HIRBlock n:
                Console.WriteLine($"{fullPrefix}");
                PrintSymbols(depth + 1, n.Variables, "Variable");
                PrintChildrenAst(depth + 1, n.Stmts);
                break;
            case HIRExprLocalRef n:
                Console.WriteLine($"{fullPrefix}");
                PrintSymbol(depth + 1, n.Symbol, "Symbol");
                break;
            case HIRExprFuncRef n:
                Console.WriteLine($"{fullPrefix}");
                PrintSymbol(depth + 1, n.Symbol, "Symbol");
                break;
            case HIRFuncDecl n:
                Console.WriteLine($"{fullPrefix}");
                PrintSymbol(depth + 1, n.Symbol);
                PrintSymbols(depth + 1, n.Locals);
                PrintHIR(depth + 1, n.Body);
                break;
            case HIRStmtLet n:
                Console.WriteLine($"{fullPrefix}");
                PrintHIRToken(depth + 1, n.NameToken, "Name");
                if (n.TypeDecl != null)
                {
                    PrintHIR(depth + 1, n.TypeDecl);
                }

                if (n.Expr != null)
                {
                    PrintHIR(depth + 1, n.Expr);
                }

                break;
            case HIRStmtReturn n:
                Console.WriteLine($"{fullPrefix}");
                if (n.Value == null)
                {
                    break;
                }

                Console.WriteLine($"{fullPrefix}: {PrettyExpr(n.Expr)}");
                PrintHIR(depth + 1, n.Expr);

                break;
            case HIRStmtAssign n:
                Console.WriteLine($"{fullPrefix}");
                PrintHIR(depth + 1, n.Target);
                PrintHIR(depth + 1, n.Value);
                break;
            case HIRStmtExpr n:
                Console.WriteLine($"{fullPrefix}");
                PrintHIR(depth + 1, n.Expr);
                break;

            case HIRExprBinary n:
                Console.WriteLine($"{fullPrefix}({n.Op})");
                PrintHIR(depth + 1, n.Left, "Left");
                PrintHIR(depth + 1, n.Right, "Right");
                break;
            case HIRExprUnary n:
                Console.WriteLine($"{fullPrefix}({n.Op})");
                PrintHIR(depth + 1, n.Operand);
                break;
            case HIRExprZeroInit hirExprZeroInit:
                break;
            case HIRExprCall n:
                Console.WriteLine($"{fullPrefix}");
                PrintHIR(depth + 1, n.Callee);
                PrintChildrenAst(depth + 1, n.Args);
                break;
            case HIRExprCast hirExprCast:
                break;
            case HIRExprError hirExprError:
                break;
            case HIRCallArg exprCallArg:
                string nameInfo = "";
                if (exprCallArg.ArgNameToken != null)
                {
                    nameInfo = $" ({TokenValue(exprCallArg.ArgNameToken.Value)})";
                }

                Console.WriteLine($"{fullPrefix}{nameInfo}: {PrettyExpr(exprCallArg.Expr)}");
                PrintHIR(depth + 1, exprCallArg.Expr);
                break;
            case HIRExprIntConst n:
                Console.WriteLine($"{fullPrefix}: {(n.IsNegative ? "-" : "")}{TokenValue(n.LiteralToken)} | IsNegative = {n.IsNegative}");
                break;
            case HIRExprLoad hirExprLoad:
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

        Console.WriteLine($"{prefix}: {sym.Name}");
    }

    private void PrintSymbols(int depth, IReadOnlyList<Symbol> symbols, string name = "")
    {
        foreach (Symbol sym in symbols)
        {
            PrintSymbol(depth, sym, name);
        }
    }

    private void PrintChildrenAst(int depth, IReadOnlyList<HIRNode> nodes)
    {
        foreach (HIRNode node in nodes)
        {
            PrintHIR(depth, node);
        }
    }

    private string TokenValue(int tokenIndex)
    {
        return _tokens[tokenIndex].Value(_code).ToString();
    }

    private void PrintHIRToken(int depth, int token, string name)
    {
        string indent = MakeIndent(depth);
        Console.WriteLine($"{indent}{name}: \"{_tokens[token].Value(_code)}\"");
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
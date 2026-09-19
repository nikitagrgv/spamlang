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
                PrintSymbols(depth + 1, n.Variables);
                PrintChildrenAst(depth + 1, n.Stmts);
                break;
            case HIRExprLocalRef n:
                Console.WriteLine($"{fullPrefix} | IsLValue = {n.IsLValue}");
                PrintSymbol(depth + 1, n.Symbol);
                break;
            case HIRExprFuncRef n:
                Console.WriteLine($"{fullPrefix}");
                PrintChildrenAst(depth + 1, n.Params);
                if (n.ReturnType != null)
                {
                    PrintHIR(depth + 1, n.ReturnType);
                }

                break;
            case HIRFuncDecl n:
                Console.WriteLine($"{fullPrefix}");
                PrintHIRToken(depth + 1, n.NameToken, "Name");
                PrintChildrenAst(depth + 1, n.Params);
                if (n.ReturnType != null)
                {
                    PrintHIR(depth + 1, n.ReturnType);
                }

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
                if (n.Expr == null)
                {
                    Console.WriteLine($"{fullPrefix}");
                    break;
                }

                Console.WriteLine($"{fullPrefix}: {PrettyExpr(n.Expr)}");
                PrintHIR(depth + 1, n.Expr);

                break;
            case HIRStmtAssign n:
                Console.WriteLine($"{fullPrefix}: {PrettyExpr(n.Target)} = {PrettyExpr(n.Value)}");
                PrintHIR(depth + 1, n.Target);
                PrintHIR(depth + 1, n.Value);
                break;
            case HIRStmtExpr n:
                Console.WriteLine($"{fullPrefix}: {PrettyExpr(n.Expr)}");
                PrintHIR(depth + 1, n.Expr);
                break;

            case HIRExprBinary n:
                Console.WriteLine($"{fullPrefix}({n.Op}): {PrettyExpr(n)}");
                PrintHIR(depth + 1, n.Left, "Left");
                PrintHIR(depth + 1, n.Right, "Right");
                break;
            case HIRExprUnary n:
                Console.WriteLine($"{fullPrefix}({n.Op}): {PrettyExpr(n)}");
                PrintHIR(depth + 1, n.Operand);
                break;
            case HIRExprZeroInit hirExprZeroInit:
                break;
            case HIRExprCall n:
                Console.WriteLine($"{fullPrefix}: {PrettyExpr(n)}");
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

    private string PrettyExpr(HIRExpr expr)
    {
        StringBuilder ret = new();
        ret.Append('(');
        switch (expr)
        {
            case HIRExprBinary binaryExpr:
                ret.Append(PrettyExpr(binaryExpr.Left));
                ret.Append(' ');
                ret.Append(TokenUtils.ToString(binaryExpr.Op));
                ret.Append(' ');
                ret.Append(PrettyExpr(binaryExpr.Right));
                break;
            case HIRExprUnary unaryExpr:
                ret.Append(TokenUtils.ToString(unaryExpr.Op));
                ret.Append(PrettyExpr(unaryExpr.Operand));
                break;
            case HIRExprZeroInit hirExprZeroInit:
                break;
            case HIRExprCall exprCall:
                ret.Clear();
                ret.Append(PrettyExpr(exprCall.Callee));
                ret.Append('(');
                for (int i = 0; i < exprCall.Args.Count; ++i)
                {
                    if (i != 0)
                    {
                        ret.Append(", ");
                    }

                    HIRCallArg arg = exprCall.Args[i];
                    ret.Append(PrettyExpr(arg.Value));
                    ret.Append($" (index={TokenValue(arg.ParameterIndex)})");
                }

                ret.Append(')');

                return ret.ToString();
            case HIRExprCast hirExprCast:
                break;
            case HIRExprError hirExprError:
                break;
            case HIRExprFuncRef hirExprFuncRef:
                break;
            case HIRExprIntConst exprInt:
                return $"{exprInt.Value}";
            case HIRExprLoad hirExprLoad:
                break;
            case HIRExprLocalRef hirExprLocalRef:
                break;
            case HIRExprIdentifier exprIdentifier:
                return TokenValue(exprIdentifier.IdentifierToken);
            default: throw new Exception("Unknown node type: " + expr.GetType().Name);
        }

        ret.Append(')');
        return ret.ToString();
    }


    private void PrintSymbol(int depth, Symbol sym)
    {
        Console.WriteLine($"{MakeIndent(depth)}Symbol: {sym.Name}");
    }

    private void PrintSymbols(int depth, IReadOnlyList<Symbol> symbols)
    {
        foreach (Symbol sym in symbols)
        {
            PrintSymbol(depth, sym);
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
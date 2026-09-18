using System.Text;
using Spamlang.Frontend;

namespace Spamlang.DebugPrinters;

public class AstPrinter
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly string _code;

    public static void Print(CompilationUnit unit, IReadOnlyList<Token> tokens, string code)
    {
        AstPrinter printer = new(tokens, code);
        printer.PrintAst(unit);
    }

    private AstPrinter(IReadOnlyList<Token> tokens, string code)
    {
        _code = code;
        _tokens = tokens;
    }

    private void PrintAst(CompilationUnit unit)
    {
        PrintAst(0, unit);
    }

    private string MakeIndent(int depth)
    {
        string indent = "";
        for (int i = 0; i < depth; i++)
        {
            indent += " |   ";
        }

        return indent;
    }

    private void PrintAst(int depth, Node node, string prefix = "")
    {
        string fullPrefix = MakeIndent(depth);
        if (prefix != "")
        {
            fullPrefix += prefix + ": ";
        }

        switch (node)
        {
            case CompilationUnit n:
                Console.WriteLine($"{fullPrefix}CompilationUnit");
                n.FuncDecls.ForEach(fd => PrintAst(depth + 1, fd));
                break;
            case Block n:
                Console.WriteLine($"{fullPrefix}Block");
                n.Stmts.ForEach(stmt => PrintAst(depth + 1, stmt));
                break;
            case Param n:
                Console.WriteLine($"{fullPrefix}Param | Type {n.Type}");
                PrintSymbol(depth + 1, n.Symbol);
                PrintAstToken(depth + 1, n.NameToken, "Name");
                PrintAst(depth + 1, n.Type);
                break;
            case IdentifierTypeNode n:
                Console.WriteLine($"{fullPrefix}IdentifierTypeNode | Type {n.ResolvedType}");
                PrintAstToken(depth + 1, n.TypeNameToken, "Type");
                break;
            case FuncTypeNode n:
                Console.WriteLine($"{fullPrefix}FuncTypeNode | Type {n.ResolvedType}");
                n.Params.ForEach(p => PrintAst(depth + 1, p));
                if (n.ReturnType != null)
                {
                    PrintAst(depth + 1, n.ReturnType);
                }

                break;
            case PointerTypeNode n:
                Console.WriteLine($"{fullPrefix}PointerTypeNode | Type {n.ResolvedType}");
                PrintAst(depth + 1, n.Pointee);
                break;
            case FuncDecl n:
                Console.WriteLine($"{fullPrefix}FuncDecl");
                PrintSymbol(depth + 1, n.Symbol);
                PrintAstToken(depth + 1, n.NameToken, "Name");
                n.Params.ForEach(p => PrintAst(depth + 1, p));
                if (n.ReturnType != null)
                {
                    PrintAst(depth + 1, n.ReturnType);
                }

                PrintAst(depth + 1, n.Body);
                break;
            case StmtLet n:
                Console.WriteLine($"{fullPrefix}StmtLet");
                PrintSymbol(depth + 1, n.Symbol);
                PrintAstToken(depth + 1, n.NameToken, "Name");
                if (n.TypeDecl != null)
                {
                    PrintAst(depth + 1, n.TypeDecl);
                }

                if (n.Expr != null)
                {
                    PrintAst(depth + 1, n.Expr);
                }

                break;
            case StmtReturn n:
                if (n.Expr == null)
                {
                    Console.WriteLine($"{fullPrefix}StmtReturn");
                    break;
                }

                Console.WriteLine($"{fullPrefix}StmtReturn: {PrettyExpr(n.Expr)}");
                PrintAst(depth + 1, n.Expr);

                break;

            case StmtAssign n:
                Console.WriteLine(
                    $"{fullPrefix}StmtAssign: {PrettyExpr(n.Target)} = {PrettyExpr(n.Value)}");
                PrintAst(depth + 1, n.Target);
                PrintAst(depth + 1, n.Value);
                break;
            case StmtExpr n:
                Console.WriteLine($"{fullPrefix}StmtExpr: {PrettyExpr(n.Expr)}");
                PrintAst(depth + 1, n.Expr);
                break;

            case ExprBinary n:
                Console.WriteLine($"{fullPrefix}BinaryExpr({n.Op}): {PrettyExpr(n)} | Type = {n.ResolvedType}");
                PrintAst(depth + 1, n.Left, "Left");
                PrintAst(depth + 1, n.Right, "Right");
                break;
            case ExprUnary n:
                Console.WriteLine($"{fullPrefix}UnaryExpr({n.Op}): {PrettyExpr(n)} | Type = {n.ResolvedType}");
                PrintAst(depth + 1, n.Operand);
                break;
            case ExprCall n:
                Console.WriteLine($"{fullPrefix}Call: {PrettyExpr(n)} | Type = {n.ResolvedType}");
                PrintAst(depth + 1, n.Callee);
                n.Args.ForEach(arg => PrintAst(depth + 1, arg.Expr));
                break;
            case ExprInt n:
                Console.WriteLine(
                    $"{fullPrefix}ExprInt: {(n.IsNegative ? "-" : "")}{TokenValue(n.LiteralToken)} | Type = {n.ResolvedType} | Value = {n.Value} | IsNegative = {n.IsNegative}");
                break;
            case ExprIdentifier n:
                Console.WriteLine(
                    $"{fullPrefix}ExprIdentifier: {TokenValue(n.IdentifierToken)} | Type = {n.ResolvedType}");
                break;
            default: throw new Exception("Unknown node type: " + node.GetType().Name);
        }
    }

    private string TokenValue(int tokenIndex)
    {
        return _tokens[tokenIndex].Value(_code).ToString();
    }

    private string PrettyExpr(Expr expr)
    {
        StringBuilder ret = new();
        ret.Append('(');
        switch (expr)
        {
            case ExprBinary binaryExpr:
                ret.Append(PrettyExpr(binaryExpr.Left));
                ret.Append(' ');
                ret.Append(TokenUtils.ToString(binaryExpr.Op));
                ret.Append(' ');
                ret.Append(PrettyExpr(binaryExpr.Right));
                break;
            case ExprUnary unaryExpr:
                ret.Append(TokenUtils.ToString(unaryExpr.Op));
                ret.Append(PrettyExpr(unaryExpr.Operand));
                break;
            case ExprCall exprCall:
                ret.Clear();
                ret.Append(PrettyExpr(exprCall.Callee));
                ret.Append('(');
                for (int i = 0; i < exprCall.Args.Count; ++i)
                {
                    if (i != 0)
                    {
                        ret.Append(", ");
                    }

                    ExprCallArg arg = exprCall.Args[i];
                    if (arg.ArgNameToken != null)
                    {
                        ret.Append(TokenValue(arg.ArgNameToken.Value));
                        ret.Append(": ");
                    }

                    ret.Append(PrettyExpr(arg.Expr));
                }

                ret.Append(')');

                return ret.ToString();
            case ExprInt exprInt:
                return (exprInt.IsNegative ? "-" : "") + TokenValue(exprInt.LiteralToken);
            case ExprIdentifier exprIdentifier:
                return TokenValue(exprIdentifier.IdentifierToken);
            default: throw new Exception("Unknown node type: " + expr.GetType().Name);
        }

        ret.Append(')');
        return ret.ToString();
    }

    private void PrintAstToken(int depth, int token, string name)
    {
        string indent = MakeIndent(depth);
        Console.WriteLine($"{indent}{name}: \"{_tokens[token].Value(_code)}\"");
    }

    private void PrintSymbol(int depth, Symbol? symbol)
    {
        string indent = MakeIndent(depth);
        Console.WriteLine($"{indent}Symbol: {symbol}");
    }
}
using System.Diagnostics;

namespace Compiler;

public class Compiler
{
    public class Flags
    {
        public bool DebugLexer = false;
        public bool DebugLexerPretty = false;
        public bool DebugParser = false;
        public bool DebugSema = false; // TODO: Unused, remove?
        public bool DebugIR = false;
        public bool DebugTimer = false;
    }

    private readonly IFileSystem _fs;
    private readonly Flags _flags;

    private readonly Diagnostic _diag = new();
    private string _code = "";
    private List<Token> _tokens = [];

    public Compiler(IFileSystem fs, Flags flags)
    {
        _fs = fs;
        _flags = flags;
    }

    public bool Compile(string file, string? output)
    {
        string fullPathFile = _fs.ResolveToFullPath(file);
        string fullPathOutput = _fs.ResolveToFullPath(output ?? "output.o");

        Console.WriteLine($"Compiling {fullPathFile} to {fullPathOutput}");

        string code = _fs.ReadAllText(fullPathFile);
        return Compile(code);
    }

    private bool Compile(string code)
    {
        Stopwatch sw = new();

        _code = code;
        _diag.Clear();

        sw.Restart();
        Lexer lexer = new();
        Lexer.Result lexerResult = lexer.Run(_code, _diag);
        TimeSpan dtLexer = sw.Elapsed;

        if (lexerResult.HasErrors)
        {
            Console.WriteLine("Lexer had errors");
        }

        _tokens = lexerResult.Tokens;

        if (_flags.DebugLexer)
        {
            Console.WriteLine("================================");
            PrintTokens();
            Console.WriteLine("================================");
        }

        if (_flags.DebugLexerPretty)
        {
            Console.WriteLine("================================");
            PrintTokensPretty();
            Console.WriteLine("================================");
        }


        sw.Restart();
        Parser parser = new();
        Parser.Result parserResult = parser.Run(_code, _tokens, _diag);
        TimeSpan dtParser = sw.Elapsed;

        if (parserResult.HasErrors)
        {
            Console.WriteLine("Parser had errors");
        }

        if (parserResult.HasErrors)
        {
            if (_flags.DebugParser)
            {
                Console.WriteLine("================================");
                PrintAst(parserResult.CompilationUnit);
                Console.WriteLine("================================");
            }

            _diag.Report();

            ReportTime("Lexer", dtLexer);
            ReportTime("Parser", dtParser);
            return false;
        }

        sw.Restart();
        Sema sema = new(_code, _tokens, _diag);
        sema.Run(parserResult.CompilationUnit);
        TimeSpan dtSema = sw.Elapsed;

        // Print after sema to include sema info
        if (_flags.DebugParser)
        {
            Console.WriteLine("================================");
            PrintAst(parserResult.CompilationUnit);
            Console.WriteLine("================================");
        }

        if (_diag.HasErrors)
        {
            _diag.Report();
            Console.WriteLine("Sema had errors");
            ReportTime("Lexer", dtLexer);
            ReportTime("Parser", dtParser);
            ReportTime("Sema", dtSema);
            return false;
        }

        IRGen irGen = new(_code, _tokens, _diag);
        IRModule irModule = irGen.Run(parserResult.CompilationUnit);

        if (_flags.DebugIR)
        {
            Console.WriteLine("================================");
            PrintIR(irModule);
            Console.WriteLine("================================");
        }

        _diag.Report();

        ReportTime("Lexer", dtLexer);
        ReportTime("Parser", dtParser);
        ReportTime("Sema", dtSema);

        return !_diag.HasErrors;
    }

    void ReportTime(string what, TimeSpan dt)
    {
        if (!_flags.DebugTimer)
        {
            return;
        }

        Console.WriteLine($"{what} Time: {dt.Milliseconds}ms");
    }

    private void PrintTokens()
    {
        foreach (Token token in _tokens)
        {
            Console.WriteLine(token.ToString(_code));
        }
    }

    private void PrintTokensPretty()
    {
        int curLine = 0;
        int curColumn = 1;
        foreach (Token token in _tokens)
        {
            while (curLine < token.Line)
            {
                curLine++;
                curColumn = 1;
                Console.WriteLine();
                Console.Write($"{curLine,5}:  ");
            }

            if (curColumn >= token.Column)
            {
                Console.Write(" ");
                curColumn = token.Column;
            }

            while (curColumn < token.Column)
            {
                Console.Write(" ");
                curColumn++;
            }

            string str = token.Type.PrettyName();

            if (token.Type.IsLiteral || token.Type == TokenType.Identifier)
            {
                str += token.Value(_code).ToString();
            }

            Console.Write(str);
            curColumn += str.Length;
        }

        Console.WriteLine();
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
            case TypeDecl n:
                Console.WriteLine($"{fullPrefix}TypeDecl | Type {n.ResolvedType}");
                PrintAstToken(depth + 1, n.TypeNameToken, "Type");
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
                Console.WriteLine($"{fullPrefix}BinaryExpr: {PrettyExpr(n)} | Type = {n.ResolvedType}");
                PrintAstToken(depth + 1, n.OperatorToken, "Operator");
                PrintAst(depth + 1, n.Left, "Left");
                PrintAst(depth + 1, n.Right, "Right");
                break;
            case ExprUnary n:
                Console.WriteLine($"{fullPrefix}UnaryExpr: {PrettyExpr(n)} | Type = {n.ResolvedType}");
                PrintAstToken(depth + 1, n.OperatorToken, "Operator");
                PrintAst(depth + 1, n.Expr);
                break;
            case ExprCall n:
                Console.WriteLine($"{fullPrefix}Call: {PrettyExpr(n)} | Type = {n.ResolvedType}");
                PrintAst(depth + 1, n.Callee);
                n.Args.ForEach(arg => PrintAst(depth + 1, arg));
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
        string ret = "(";
        switch (expr)
        {
            case ExprBinary binaryExpr:
                ret += PrettyExpr(binaryExpr.Left);
                ret += " ";
                ret += TokenValue(binaryExpr.OperatorToken);
                ret += " ";
                ret += PrettyExpr(binaryExpr.Right);
                break;
            case ExprUnary unaryExpr:
                ret += TokenValue(unaryExpr.OperatorToken);
                ret += PrettyExpr(unaryExpr.Expr);
                break;
            case ExprCall exprCall:
                ret = "";
                ret += PrettyExpr(exprCall.Callee);
                ret += "(";
                for (int i = 0; i < exprCall.Args.Count; ++i)
                {
                    if (i != 0)
                    {
                        ret += ", ";
                    }

                    ret += PrettyExpr(exprCall.Args[i]);
                }

                ret += ")";

                return ret;
            case ExprInt exprInt:
                return (exprInt.IsNegative ? "-" : "") + TokenValue(exprInt.LiteralToken);
            case ExprIdentifier exprIdentifier:
                return TokenValue(exprIdentifier.IdentifierToken);
            default: throw new Exception("Unknown node type: " + expr.GetType().Name);
        }

        ret += ")";
        return ret;
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

    private void PrintIR(IRModule module)
    {
        foreach (IRFunction func in module.Functions)
        {
            PrintIR(func);
        }
    }

    private void PrintIR(IRFunction func)
    {
        int counter = -1;
        Console.Write($"fn @{func.Name}(");
        for (int i = 0; i < func.Params.Count; i++)
        {
            IRParam param = func.Params[i];
            if (i != 0)
            {
                Console.Write(", ");
            }

            param.Id = ++counter;
            Console.Write($"{param.Type} %{param.Id}");
        }

        Console.Write($") -> {func.Type.ReturnType}");
        Console.WriteLine();
        Console.WriteLine("{");
        foreach (IRBasicBlock bb in func.BasicBlocks)
        {
            PrintIR(bb, ref counter);
        }

        Console.WriteLine("}");
    }

    private void PrintIR(IRBasicBlock bb, ref int counter)
    {
        Console.WriteLine($"{bb.Name}:");
        foreach (IRInstruction inst in bb.Instructions)
        {
            if (inst.Type != BuiltinType.Void)
            {
                inst.Id = ++counter;
            }

            Console.WriteLine($"  {inst.PrintDefinition()}");
        }
    }
}
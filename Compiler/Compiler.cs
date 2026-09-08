using System.Diagnostics;
using Compiler.printers;

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
            TokensPrinter.Print(_tokens, _code);
            Console.WriteLine("================================");
        }

        if (_flags.DebugLexerPretty)
        {
            Console.WriteLine("================================");
            TokensPrinter.PrintPretty(_tokens, _code);
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
                AstPrinter.Print(parserResult.CompilationUnit, _tokens, _code);
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
            AstPrinter.Print(parserResult.CompilationUnit, _tokens, _code);
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

        sw.Restart();
        IRGen irGen = new();
        IRModule irModule = irGen.Run(parserResult.CompilationUnit);
        TimeSpan dtIRGen = sw.Elapsed;

        if (_flags.DebugIR)
        {
            Console.WriteLine("================================");
            IRPrinter.Print(irModule);
            Console.WriteLine("================================");
        }

        _diag.Report();

        ReportTime("Lexer", dtLexer);
        ReportTime("Parser", dtParser);
        ReportTime("Sema", dtSema);
        ReportTime("IR", dtIRGen);

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
}
using System.Diagnostics;
using Spamlang.Printers;

namespace Spamlang;

public class Compiler
{
    public class Flags
    {
        public bool DebugLexer = false;
        public bool DebugLexerPretty = false;
        public bool DebugParser = false;
        public bool DebugIR = false;
        public bool DebugMIR = false;
        public bool DebugTimer = false;
    }

    private readonly IFileSystem _fs;
    private readonly Flags _flags;
    private readonly string _clangPath;
    private readonly string _buildPath;

    public Compiler(IFileSystem fs, Flags flags, string clangPath, string buildPath)
    {
        _fs = fs;
        _flags = flags;
        _clangPath = clangPath;
        _buildPath = buildPath;
    }

    public bool Compile(string file, string output, bool compileOnly)
    {
        Stopwatch totalSw = Stopwatch.StartNew();
        Stopwatch sw = new();

        TimeSpan? readTime = null;
        TimeSpan? dtLexer = null;
        TimeSpan? dtParser = null;
        TimeSpan? dtSema = null;
        TimeSpan? dtIRGen = null;
        TimeSpan? dtCodeGen = null;

        void ReportTime(string what, TimeSpan? dt)
        {
            if (!dt.HasValue)
            {
                return;
            }

            Console.WriteLine($"{what} Time: {dt.Value.Milliseconds}ms");
        }

        void ReportTimes()
        {
            if (!_flags.DebugTimer)
            {
                return;
            }

            ReportTime("Read", readTime);
            ReportTime("Lexer", dtLexer);
            ReportTime("Parser", dtParser);
            ReportTime("Sema", dtSema);
            ReportTime("IR", dtIRGen);
            ReportTime("Codegen", dtCodeGen);
            ReportTime("Total", totalSw.Elapsed);
        }

        string fullPathFile = _fs.ResolveToFullPath(file);
        string fullPathOutput = _fs.ResolveToFullPath(output);

        Console.WriteLine($"Compiling {fullPathFile} to {fullPathOutput}");

        sw.Restart();
        string code = _fs.ReadAllText(fullPathFile);
        readTime = sw.Elapsed;

        Diagnostic diag = new();

        sw.Restart();
        Lexer lexer = new();
        Lexer.Result lexerResult = lexer.Run(code, diag);
        dtLexer = sw.Elapsed;

        if (lexerResult.HasErrors)
        {
            Console.WriteLine("Lexer had errors");
        }

        List<Token> tokens = lexerResult.Tokens;

        if (_flags.DebugLexer)
        {
            Console.WriteLine("================================================");
            TokensPrinter.Print(tokens, code);
            Console.WriteLine("================================================");
        }

        if (_flags.DebugLexerPretty)
        {
            Console.WriteLine("================================================");
            TokensPrinter.PrintPretty(tokens, code);
            Console.WriteLine("================================================");
        }

        sw.Restart();
        Parser parser = new();
        Parser.Result parserResult = parser.Run(code, tokens, diag);
        dtParser = sw.Elapsed;

        if (parserResult.HasErrors)
        {
            Console.WriteLine("Parser had errors");
        }

        if (parserResult.HasErrors)
        {
            if (_flags.DebugParser)
            {
                Console.WriteLine("================================================");
                AstPrinter.Print(parserResult.CompilationUnit, tokens, code);
                Console.WriteLine("================================================");
            }

            diag.Report();

            ReportTimes();
            return false;
        }

        sw.Restart();
        Sema sema = new(code, tokens, diag);
        sema.Run(parserResult.CompilationUnit);
        dtSema = sw.Elapsed;

        // Print after sema to include sema info
        if (_flags.DebugParser)
        {
            Console.WriteLine("================================================");
            AstPrinter.Print(parserResult.CompilationUnit, tokens, code);
            Console.WriteLine("================================================");
        }

        if (diag.HasErrors)
        {
            diag.Report();
            Console.WriteLine("Sema had errors");
            ReportTimes();
            return false;
        }

        sw.Restart();
        IRGen irGen = new();
        IRModule irModule = irGen.Run(parserResult.CompilationUnit);
        dtIRGen = sw.Elapsed;

        if (_flags.DebugIR)
        {
            Console.WriteLine("================================================");
            IRPrinter.Print(irModule);
            Console.WriteLine("================================================");
        }

        sw.Restart();
        Codegen codegen = new();
        MModule mmodule = codegen.Run(irModule);
        dtCodeGen = sw.Elapsed;

        if (_flags.DebugMIR)
        {
            Console.WriteLine("================================================");
            MIRPrinter.Print(mmodule, Console.Out);
            Console.WriteLine("================================================");
        }

        diag.Report();
        ReportTimes();

        return !diag.HasErrors;
    }
}
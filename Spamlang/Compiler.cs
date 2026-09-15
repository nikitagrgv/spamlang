using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Spamlang.Backend;
using Spamlang.DebugPrinters;
using Spamlang.Frontend;

namespace Spamlang;

public class Compiler
{
    public class Flags
    {
        public required bool DebugLexer = false;
        public required bool DebugLexerPretty = false;
        public required bool DebugParser = false;
        public required bool DebugIR = false;
        public required bool DebugMIR = false;
        public required bool DebugTimer = false;
        public required bool Verbose = false;
        public required bool DebugInfo = false;
    }

    private readonly IFileSystem _fs;
    private readonly Flags _flags;
    private readonly string _clangPath;
    private readonly string _buildPath;
    private readonly string? _emitAsmPath;

    public Compiler(IFileSystem fs, Flags flags, string clangPath, string buildPath, string? emitAsmPath)
    {
        _fs = fs;
        _flags = flags;
        _clangPath = clangPath;
        _buildPath = buildPath;
        _emitAsmPath = emitAsmPath;
    }

    public bool Compile(string file, string output, bool compileOnly)
    {
        using Timers timers = new(_flags.DebugTimer ? Console.Out : null);

        string fullPathInput = _fs.ResolveToFullPath(file);
        string fullPathOutput = _fs.ResolveToFullPath(output);
        EnsureFileDir(fullPathOutput);

        if (_flags.Verbose)
        {
            Console.WriteLine($"Compiling {fullPathInput} to {fullPathOutput}");
        }

        timers.RestartTimer();
        string code;
        try
        {
            code = _fs.ReadAllText(fullPathInput);
        }
        catch (Exception)
        {
            Console.Error.WriteLine($"Failed to read file {fullPathInput}");
            return false;
        }

        timers.FinishTimer("Read");

        Diagnostic diag = new();
        TypeRegistry typeRegistry = new();

        Frontend.Frontend.Result frontendResult = Frontend.Frontend.Run(code, typeRegistry, diag, timers);

        PrintLexer(frontendResult.Tokens, code);
        PrintLexerPretty(frontendResult.Tokens, code);
        PrintAst(frontendResult.Tokens, code, frontendResult.CompilationUnit);

        if (diag.HasErrors)
        {
            ReportDiag(diag);
            return false;
        }

        Backend.Backend.Result backendResult = Backend.Backend.Run(frontendResult.CompilationUnit, typeRegistry, timers);

        PrintIR(backendResult.IRModule);
        PrintMIR(backendResult.MModule);

        ReportDiag(diag);

        timers.RestartTimer();
        string asmPath = SaveAsm(backendResult.MModule, fullPathInput);
        timers.FinishTimer("Save Asm");

        if (_emitAsmPath != null)
        {
            string dst = _fs.ResolveToFullPath(_emitAsmPath);
            _fs.CopyFile(asmPath, dst);
        }

        timers.RestartTimer();
        // Only one target is supported for now
        string target = "x86_64-pc-windows-msvc";
        string? objPath = CompileAsm(asmPath, target);
        timers.FinishTimer("Compile Asm");

        if (objPath == null)
        {
            Console.Error.WriteLine("Failed to compile asm");
            return false;
        }

        if (compileOnly)
        {
            _fs.CopyFile(objPath, fullPathOutput);
            return true;
        }

        timers.RestartTimer();
        bool linkOk = LinkObj(objPath, fullPathOutput);
        timers.FinishTimer("Link");

        if (!linkOk)
        {
            Console.Error.WriteLine("Failed to link");
            return false;
        }

        return true;
    }

    private string SaveAsm(MModule mmodule, string file)
    {
        // TODO: Shitty
        string name = Path.GetFileNameWithoutExtension(file);
        name += $"-{Guid.NewGuid():N}";
        name += ".s";
        string path = Path.Join(_buildPath, name);

        UTF8Encoding encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        using StreamWriter stream = new(_fs.OpenWrite(path), encoding);
        MIRPrinter.Print(mmodule, stream);

        if (_flags.Verbose)
        {
            Console.WriteLine($"Assembly saved to {path}");
        }

        return path;
    }

    private string? CompileAsm(string asmPath, string target)
    {
        List<string> args = new();

        string name = Path.GetFileNameWithoutExtension(asmPath) + ".o";
        string outPath = Path.Join(_buildPath, name);

        if (_flags.DebugInfo)
        {
            args.Add("-g");
        }

        args.Add("-c");

        args.Add($"--target={target}");

        args.Add("-x");
        args.Add("assembler");

        args.Add(asmPath);

        args.Add("-o");
        args.Add(outPath);

        int code = RunApp(_clangPath, args, _flags.Verbose);
        if (code != 0)
        {
            return null;
        }

        return outPath;
    }

    private bool LinkObj(string objPath, string outputPath)
    {
        List<string> args = new();

        if (_flags.DebugInfo)
        {
            args.Add("-g");
        }

        args.Add(objPath);

        args.Add("-o");
        args.Add(outputPath);

        int code = RunApp(_clangPath, args, _flags.Verbose);
        return code == 0;
    }


    private static int RunApp(string exe, IReadOnlyList<string> args, bool verbose)
    {
        if (verbose)
        {
            Console.WriteLine($"{exe} {string.Join(" ", args)}");
        }

        ProcessStartInfo psi = new()
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using Process p = new();
        p.StartInfo = psi;
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                Console.Out.WriteLine(e.Data);
            }
        };
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                Console.Error.WriteLine(e.Data);
            }
        };

        try
        {
            p.Start();
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 2)
        {
            throw new Exception($"Could not find {exe}. Set SPAM_CLANG_PATH or pass --clang <path>");
        }

        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();

        return p.ExitCode;
    }

    private void EnsureFileDir(string fullPathOutput)
    {
        string? dir = Path.GetDirectoryName(fullPathOutput);
        if (dir != null)
        {
            _fs.CreateDir(dir);
        }
    }

    private void ReportDiag(Diagnostic diag)
    {
        foreach (DiagnosticEntry entry in diag.Entries)
        {
            Console.Error.WriteLine(entry.PrettyString());
        }
    }

    private void PrintLexer(List<Token> tokens, string code)
    {
        if (_flags.DebugLexer)
        {
            PrintSeparator();
            TokensPrinter.Print(tokens, code);
            PrintSeparator();
        }
    }

    private void PrintLexerPretty(List<Token> tokens, string code)
    {
        if (_flags.DebugLexerPretty)
        {
            PrintSeparator();
            TokensPrinter.PrintPretty(tokens, code);
            PrintSeparator();
        }
    }

    private void PrintAst(List<Token> tokens, string code, CompilationUnit compilationUnit)
    {
        if (_flags.DebugParser)
        {
            PrintSeparator();
            AstPrinter.Print(compilationUnit, tokens, code);
            PrintSeparator();
        }
    }

    private void PrintIR(IRModule irModule)
    {
        if (_flags.DebugIR)
        {
            PrintSeparator();
            IRPrinter.Print(irModule);
            PrintSeparator();
        }
    }

    private void PrintMIR(MModule mmodule)
    {
        if (_flags.DebugMIR)
        {
            PrintSeparator();
            MIRPrinter.Print(mmodule, Console.Out);
            PrintSeparator();
        }
    }

    private static void PrintSeparator()
    {
        Console.WriteLine("================================================");
    }
}
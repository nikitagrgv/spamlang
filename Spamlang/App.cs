namespace Spamlang;

class App
{
    struct Arguments
    {
        [Argument("output", "o")]
        public string? Output { get; set; }

        [Argument("verbose", "v")]
        public bool Verbose { get; set; }

        [Argument("compile-only", "c")]
        public bool CompileOnly { get; set; }

        [Argument("debug-info", "g")]
        public bool DebugInfo { get; set; }

        [Argument("clang-path")]
        public string? ClangPath { get; set; }

        [Argument("emit-asm")]
        public string? EmitAsmPath { get; set; }

        [Argument("lexer")]
        public bool DebugLexer { get; set; }

        [Argument("lexer-pretty")]
        public bool DebugLexerPretty { get; set; }

        [Argument("parser")]
        public bool DebugParser { get; set; }

        [Argument("ir")]
        public bool DebugIR { get; set; }

        [Argument("mir")]
        public bool DebugMIR { get; set; }

        [Argument("timer")]
        public bool DebugTimer { get; set; }

        [PositionalArgsList]
        public List<string> Files { get; set; }
    }

    private static Compiler.Flags GetFlags(Arguments arguments)
    {
        Compiler.Flags flags = new()
        {
            DebugLexer = arguments.DebugLexer,
            DebugLexerPretty = arguments.DebugLexerPretty,
            DebugParser = arguments.DebugParser,
            DebugIR = arguments.DebugIR,
            DebugMIR = arguments.DebugMIR,
            DebugTimer = arguments.DebugTimer,
            Verbose = arguments.Verbose,
            DebugInfo = arguments.DebugInfo,
        };
        return flags;
    }

    static int Main(string[] args)
    {
        ArgumentsParser.Result<Arguments> result = ArgumentsParser.Parse<Arguments>(args);
        if (result.Errors.Count > 0)
        {
            foreach (string error in result.Errors)
            {
                Console.WriteLine($"{error}");
            }

            return 1;
        }

        Arguments arguments = result.Value;
        if (arguments.Files.Count != 1)
        {
            Console.WriteLine("Expected exactly one file");
            return 1;
        }

        string curDir = Directory.GetCurrentDirectory();
        FileSystem fs = new(curDir);

        string? clangPath = arguments.ClangPath;
        if (string.IsNullOrEmpty(clangPath))
        {
            clangPath = Environment.GetEnvironmentVariable("SPAMLANG_CLANG_PATH");
        }

        if (string.IsNullOrEmpty(clangPath))
        {
            clangPath = "clang";
        }

        string buildPath = CreateBuildDir(fs);
        Compiler.Flags flags = GetFlags(arguments);
        Compiler compiler = new(fs, flags, clangPath, buildPath, arguments.EmitAsmPath);

        string? output = arguments.Output;
        if (string.IsNullOrEmpty(output))
        {
            output = "out";
            // TODO: Linux!
            output += arguments.CompileOnly ? ".o" : ".exe";
        }

        try
        {
            bool ok = compiler.Compile(arguments.Files[0], output, arguments.CompileOnly);
            fs.RemoveDir(buildPath);
            return ok ? 0 : 1;
        }
        catch (Exception e)
        {
            // NOTE: Leave the build dir for investigation 
            Console.WriteLine("Unexpected exception: " + e);
            Console.WriteLine("See build dir: " + buildPath);
            return 1;
        }
    }

    private static string CreateBuildDir(IFileSystem fs)
    {
        string dirName = $"spamlang-{Environment.ProcessId}-{Guid.NewGuid():N}";
        string path = fs.CreateTempDir(dirName);
        return path;
    }
}
namespace Compiler;

class App
{
    struct Arguments
    {
        [Argument("output", "o")]
        public string? Output { get; set; }

        [Argument("lexer")]
        public bool DebugLexer { get; set; }

        [Argument("lexer-pretty")]
        public bool DebugLexerPretty { get; set; }

        [Argument("parser")]
        public bool DebugParser { get; set; }

        [Argument("sema")]
        public bool DebugSema { get; set; }

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
            DebugSema = arguments.DebugSema,
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

        Compiler.Flags flags = GetFlags(arguments);
        Compiler compiler = new(fs, flags);
        bool ok = compiler.Compile(arguments.Files[0], arguments.Output);
        return ok ? 0 : 1;
    }
}
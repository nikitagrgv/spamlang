using Spamlang.DebugPrinters;
using Spamlang.Frontend;

namespace Tests;

public class SemaTests
{
    private class Tester
    {
        private readonly string _code;
        private readonly Diagnostic _diag;
        private readonly List<Token> _tokens;
        private readonly HIRStmt _stmt;

        public string Code => _code;
        public Diagnostic Diag => _diag;
        public bool HasErrors => _diag.HasErrors;

        public static Tester Parse(string stmtCode)
        {
        }

        private Tester(string stmtCode)
        {
            _code = $$"""
                      fn main() -> i32
                      {
                          {{stmtCode}};
                      }
                      """;
            _diag = new Diagnostic();

            Lexer lexer = new(_code, _diag);
            _tokens = lexer.Run();

            Parser parser = new(_code, _tokens, _diag);
            CompilationUnit unit = parser.Run();

            TypeRegistry tr = new();
            Sema sema = new(_code, _tokens, _diag, tr);
            HIRCompilationUnit hirUnit = sema.Run(unit);

            Assert.Single(hirUnit.FuncDecls);
            HIRFuncDecl main = hirUnit.FuncDecls[0];
            Assert.Equal("main", main.Symbol.Name);

            Assert.Single(main.Body.Stmts);
            HIRStmt stmt = main.Body.Stmts[0];

            _stmt = stmt;
        }
    }
}
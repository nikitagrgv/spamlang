using Spamlang.DebugPrinters;
using Spamlang.Frontend;

namespace Tests;

public class SemaTests
{
    [Fact]
    public void Sema_BuildsSimpleProgram()
    {
        string code = $$"""
                        fn main() -> i32
                        {
                            let a: i16 = 111;
                            a = foo(a + 222);
                            return 333;
                        }

                        fn foo(value: i64) -> i8
                        {
                            return value as i8 + 10;
                        }
                        """;
        Tester tester = Tester.BuildCode(code);
        Assert.False(tester.HasErrors, tester.DumpErrors);

        HIRCompilationUnit unit = tester.Unit;
        Assert.Equal(2, unit.FuncDecls.Count);

        // Check main
        HIRFuncDecl main = unit.FuncDecls[0];
        Assert.Equal("main", main.Symbol.Name);

        Assert.Equal(3, main.Body.Stmts.Count);
        
    }

    private class Tester
    {
        private readonly string _code;
        private readonly Diagnostic _diag;
        private readonly List<Token> _tokens;
        private readonly HIRStmt? _stmt;
        private readonly HIRCompilationUnit _unit;

        private enum Kind
        {
            Code,
            Stmt,
        }

        public string Code => _code;
        public Diagnostic Diag => _diag;
        public HIRCompilationUnit Unit => _unit;
        public bool HasErrors => _diag.HasErrors;
        public string DumpErrors => _diag.PrettyString();

        public HIRStmt Stmt
        {
            get
            {
                Assert.NotNull(_stmt);
                return _stmt;
            }
        }

        public HIRStmtLet StmtLet
        {
            get
            {
                Assert.IsType<HIRStmtLet>(_stmt);
                return (HIRStmtLet)_stmt;
            }
        }

        public HIRStmtExpr StmtExpr
        {
            get
            {
                Assert.IsType<HIRStmtExpr>(_stmt);
                return (HIRStmtExpr)_stmt;
            }
        }

        public static Tester BuildCode(string code)
        {
            Tester tester = new(code, Kind.Code);
            return tester;
        }

        public static Tester BuildStmt(string stmtCode)
        {
            Tester tester = new(stmtCode, Kind.Stmt);
            return tester;
        }

        private Tester(string code, Kind kind)
        {
            if (kind == Kind.Stmt)
            {
                _code = $$"""
                          fn main() -> i32
                          {
                              {{code}};
                          }
                          """;
            }
            else
            {
                _code = code;
            }

            _diag = new Diagnostic();

            Lexer lexer = new(_code, _diag);
            _tokens = lexer.Run();

            Parser parser = new(_code, _tokens, _diag);
            CompilationUnit unit = parser.Run();

            TypeRegistry tr = new();
            Sema sema = new(_code, _tokens, _diag, tr);
            _unit = sema.Run(unit);

            if (kind == Kind.Stmt)
            {
                Assert.Single(_unit.FuncDecls);
                HIRFuncDecl main = _unit.FuncDecls[0];
                Assert.Equal("main", main.Symbol.Name);

                Assert.Single(main.Body.Stmts);
                HIRStmt stmt = main.Body.Stmts[0];

                _stmt = stmt;
            }
        }
    }
}
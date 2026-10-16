using Spamlang.DebugPrinters;
using Spamlang.Frontend;

namespace Tests;

public class SemaTests
{
    [Fact]
    public void Sema_BuildsSimpleProgram()
    {
        string code = """
                      fn main() -> i32
                      {
                          let a: i16 = 111;
                          a = foo(value: a + 222);
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

        HIRFuncDecl main = unit.FuncDecls[0];
        FuncSymbol mainSym = main.Symbol;
        Assert.Equal("main", mainSym.Name);

        HIRFuncDecl foo = unit.FuncDecls[1];
        FuncSymbol fooSym = foo.Symbol;
        Assert.Equal("foo", fooSym.Name);

        // main
        Assert.Equal(3, main.Body.Stmts.Count);

        // let a: i16 = 111;
        HIRStmtLet letA = Assert.IsType<HIRStmtLet>(main.Body.Stmts[0]);
        Assert.Equal("a", letA.VariableSymbol.Name);
        Assert.Equal(BuiltinType.I16, letA.VariableSymbol.Type);

        HIRExprIntConst initA = Assert.IsType<HIRExprIntConst>(letA.Init);
        Assert.Equal(BuiltinType.I16, initA.Type);
        Assert.Equal(111, initA.Value);

        // a = foo(value: a + 222);
        HIRStmtAssign assignA = Assert.IsType<HIRStmtAssign>(main.Body.Stmts[1]);

        HIRExprLocalRef assignATarget = Assert.IsType<HIRExprLocalRef>(assignA.Target);
        Assert.Equal(letA.VariableSymbol, assignATarget.Symbol);

        HIRExprCast assignACast = Assert.IsType<HIRExprCast>(assignA.Value);
        Assert.True(assignACast.IsSynthesized);
        Assert.Equal(BuiltinType.I16, assignACast.Type);

        HIRExprCall assignACall = Assert.IsType<HIRExprCall>(assignACast.Value);
        Assert.Equal(BuiltinType.I8, assignACall.Type);

        HIRExprFuncRef assignACallCallee = Assert.IsType<HIRExprFuncRef>(assignACall.Callee);
        Assert.Equal(fooSym, assignACallCallee.Symbol);

        Assert.Single(assignACall.Args);
        HIRCallArg assignACallArg = assignACall.Args[0];
        Assert.Equal(0, assignACallArg.ParameterIndex);

        HIRExprBinary assignACallArgValue = Assert.IsType<HIRExprBinary>(assignACallArg.Value);
        Assert.Equal(BuiltinType.I64, assignACallArgValue.Type);

        HIRExprCast leftCast = Assert.IsType<HIRExprCast>(assignACallArgValue);
        Assert.True(leftCast.IsSynthesized);
        Assert.Equal(BuiltinType.I64, leftCast.Type);

        HIRExprLoad leftLoad = Assert.IsType<HIRExprLoad>(leftCast.Value);
        Assert.Equal(BuiltinType.I16, leftLoad.Type);

        HIRExprLocalRef leftRef = Assert.IsType<HIRExprLocalRef>(leftLoad.Address);
        Assert.Equal(BuiltinType.I16, leftRef.Type);
        Assert.Equal(letA.VariableSymbol, leftRef.Symbol);
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
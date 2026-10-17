using Spamlang.DebugPrinters;
using Spamlang.Frontend;

namespace Tests;

public class SemaTests
{
    [Theory]
    // Decimal
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("123", 123)]
    [InlineData("1234567890", 1234567890)]
    [InlineData("-0", -0)]
    [InlineData("-1", -1)]
    [InlineData("-123", -123)]
    [InlineData("-1234567890", -1234567890)]
    // Octal (C# doesn't have compile-time octal literals :( )
    [InlineData("0o0", 0)]
    [InlineData("0o00", 0)]
    [InlineData("0o1", 1)]
    [InlineData("0o123", 83)]
    [InlineData("0o1234567", 342391)]
    [InlineData("-0o0", -0)]
    [InlineData("-0o00", -0)]
    [InlineData("-0o1", -1)]
    [InlineData("-0o123", -83)]
    [InlineData("-0o1234567", -342391)]
    // Hex
    [InlineData("0x0", 0x0)]
    [InlineData("0x00", 0x00)]
    [InlineData("0x01", 0x01)]
    [InlineData("0x0123", 0x0123)]
    [InlineData("0x0123456789abcdef", 0x0123456789abcdef)]
    [InlineData("0x0123456789ABCDEF", 0x0123456789ABCDEF)]
    [InlineData("-0x0", -0x0)]
    [InlineData("-0x00", -0x00)]
    [InlineData("-0x01", -0x01)]
    [InlineData("-0x0123", -0x0123)]
    [InlineData("-0x0123456789abcdef", -0x0123456789abcdef)]
    [InlineData("-0x0123456789ABCDEF", -0x0123456789ABCDEF)]
    public void Sema_ParsesLiteralInt(string str, Int128 expected)
    {
        string stmtCode = $"let a: i64 = {str}";
        Tester tester = Tester.BuildStmt(stmtCode);
        Assert.False(tester.HasErrors, tester.DumpErrors);
        HIRStmtLet stmt = tester.StmtLet;
        HIRExprIntConst exprConst = Assert.IsType<HIRExprIntConst>(stmt.Init);
        Assert.Equal(BuiltinType.I64, exprConst.Type);
        Assert.False(exprConst.IsLValue);

        Assert.Equal(expected, exprConst.Value);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("0.1", 0.1)]
    [InlineData("0.2", 0.2)]
    [InlineData("0.3", 0.3)]
    [InlineData("1.2", 1.2)]
    [InlineData("1.2e2", 1.2e2)]
    [InlineData("1.2e-2", 1.2e-2)]
    [InlineData("1.2E-2", 1.2E-2)]
    [InlineData("1.2E+2", 1.2E+2)]
    [InlineData("1e2", 1e2)]
    [InlineData("1E2", 1E2)]
    [InlineData("-0", 0)]
    [InlineData("-0.1", -0.1)]
    [InlineData("-0.2", -0.2)]
    [InlineData("-0.3", -0.3)]
    [InlineData("-1.2", -1.2)]
    [InlineData("-1.2e2", -1.2e2)]
    [InlineData("-1.2e-2", -1.2e-2)]
    [InlineData("-1.2E-2", -1.2E-2)]
    [InlineData("-1.2E+2", -1.2E+2)]
    [InlineData("-1e2", -1e2)]
    [InlineData("-1E2", -1E2)]
    public void Sema_ParsesLiteralFloat(string str, double expected)
    {
        string stmtCode = $"let a: f64 = {str}";
        Tester tester = Tester.BuildStmt(stmtCode);
        Assert.False(tester.HasErrors, tester.DumpErrors);
        HIRStmtLet stmt = tester.StmtLet;
        HIRExprFloatConst exprConst = Assert.IsType<HIRExprFloatConst>(stmt.Init);
        Assert.Equal(BuiltinType.F64, exprConst.Type);
        Assert.False(exprConst.IsLValue);

        Assert.Equal(expected, exprConst.Value);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Sema_ParsesLiteralBool(string str, bool expected)
    {
        string stmtCode = $"let a: bool = {str}";
        Tester tester = Tester.BuildStmt(stmtCode);
        Assert.False(tester.HasErrors, tester.DumpErrors);
        HIRStmtLet stmt = tester.StmtLet;
        HIRExprBoolConst exprConst = Assert.IsType<HIRExprBoolConst>(stmt.Init);
        Assert.Equal(BuiltinType.Bool, exprConst.Type);
        Assert.False(exprConst.IsLValue);

        Assert.Equal(expected, exprConst.Value);
    }

    [Theory]
    // i32
    [InlineData("0", "i32")]
    [InlineData("1", "i32")]
    [InlineData("123", "i32")]
    [InlineData("-123", "i32")]
    // f64
    [InlineData("0.0", "f64")]
    [InlineData("1.0", "f64")]
    [InlineData("1.2", "f64")]
    [InlineData("-1.2", "f64")]
    // bool
    [InlineData("true", "bool")]
    [InlineData("false", "bool")]
    public void Sema_CheckDefaultTypesForLiterals(string str, string expected)
    {
        string stmtCode = $"let a = {str}";
        Tester tester = Tester.BuildStmt(stmtCode);
        Assert.False(tester.HasErrors, tester.DumpErrors);
        HIRStmtLet stmt = tester.StmtLet;
        Assert.Equal(expected, stmt.Init.Type.Name);
        Assert.Equal(expected, stmt.VariableSymbol.Type.Name);
    }


    [Theory]
    [InlineData("-11 + 3", -11 + 3)]
    [InlineData("-11 - 3", -11 - 3)]
    [InlineData("-11 * 3", -11 * 3)]
    [InlineData("-11 / 3", -11 / 3)]
    [InlineData("-11 % 3", -11 % 3)]
    [InlineData("11 << 3", 11 << 3)]
    [InlineData("1111 >> 3", 1111 >> 3)]
    [InlineData("1111 ^ 3", 1111 ^ 3)]
    [InlineData("1111 & 3", 1111 & 3)]
    [InlineData("1111 | 3", 1111 | 3)]
    [InlineData("2 + 3 * 4 - 24 / 3", 2 + 3 * 4 - 24 / 3)]
    [InlineData("(1111 | 3) + (1 * -4) * 13 | 7", (1111 | 3) + (1 * -4) * 13 | 7)]
    public void Sema_FoldsConstIntExpression(string str, Int128 expected)
    {
        string stmtCode = $"let a: i64 = {str}";
        Tester tester = Tester.BuildStmt(stmtCode);
        Assert.False(tester.HasErrors, tester.DumpErrors);
        HIRStmtLet stmt = tester.StmtLet;
        Assert.Equal(BuiltinType.I64, stmt.Init.Type);
        Assert.Equal(BuiltinType.I64, stmt.VariableSymbol.Type);
        HIRExprIntConst init = Assert.IsType<HIRExprIntConst>(stmt.Init);
        Assert.Equal(BuiltinType.I64, init.Type);
        Assert.Equal(expected, init.Value);
    }

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
                          return value as i8;
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
        Assert.Empty(main.Symbol.Params);

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

        HIRExprCast assignAArgCast = Assert.IsType<HIRExprCast>(assignACallArg.Value);
        Assert.True(assignAArgCast.IsSynthesized);
        Assert.Equal(BuiltinType.I64, assignAArgCast.Type);

        HIRExprBinary assignABinary = Assert.IsType<HIRExprBinary>(assignAArgCast.Value);
        Assert.Equal(BuiltinType.I16, assignABinary.Type);

        HIRExprLoad leftLoad = Assert.IsType<HIRExprLoad>(assignABinary.Left);
        Assert.Equal(BuiltinType.I16, leftLoad.Type);

        HIRExprLocalRef leftRef = Assert.IsType<HIRExprLocalRef>(leftLoad.Address);
        Assert.Equal(BuiltinType.I16, leftRef.Type);
        Assert.Equal(letA.VariableSymbol, leftRef.Symbol);

        HIRExprIntConst rightConst = Assert.IsType<HIRExprIntConst>(assignABinary.Right);
        Assert.Equal(BuiltinType.I16, rightConst.Type);
        Assert.Equal(222, rightConst.Value);

        // return 333;
        HIRStmtReturn returnStmt = Assert.IsType<HIRStmtReturn>(main.Body.Stmts[2]);
        HIRExprIntConst returnExpr = Assert.IsType<HIRExprIntConst>(returnStmt.Value);
        Assert.Equal(BuiltinType.I32, returnExpr.Type);
        Assert.Equal(333, returnExpr.Value);

        // foo
        Assert.Single(foo.Body.Stmts);
        Assert.Single(foo.Symbol.Params);
        Assert.Equal("value", foo.Symbol.Params[0].Name);
        Assert.Equal(BuiltinType.I64, foo.Symbol.Params[0].Type);

        // return value as i8;
        HIRStmtReturn fooReturnStmt = Assert.IsType<HIRStmtReturn>(foo.Body.Stmts[0]);

        HIRExprCast valueCast = Assert.IsType<HIRExprCast>(fooReturnStmt.Value);
        Assert.False(valueCast.IsSynthesized);
        Assert.Equal(BuiltinType.I8, valueCast.Type);

        HIRExprLoad valueLoad = Assert.IsType<HIRExprLoad>(valueCast.Value);
        Assert.Equal(BuiltinType.I64, valueLoad.Type);

        HIRExprLocalRef valueRef = Assert.IsType<HIRExprLocalRef>(valueLoad.Address);
        Assert.Equal(foo.Symbol.Params[0], valueRef.Symbol);
        Assert.Equal(BuiltinType.I64, valueRef.Type);
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

        public HIRStmtLet StmtLet => Assert.IsType<HIRStmtLet>(_stmt);
        public HIRStmtExpr StmtExpr => Assert.IsType<HIRStmtExpr>(_stmt);

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
                              return 0;
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

                Assert.Equal(2, main.Body.Stmts.Count);
                HIRStmt stmt = main.Body.Stmts[0];

                _stmt = stmt;
            }
        }
    }
}
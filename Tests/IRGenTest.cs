using Spamlang.Backend;
using Spamlang.Frontend;
using Xunit.Abstractions;

namespace Tests;

public class IRGenTest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public IRGenTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void IRGen_SimpleProgramIsValid()
    {
        string code = """
                      fn main() -> i32
                      {
                          let val = 1 + 2;
                          checkI32(val); // 3

                          val = (3.0 + 4) as i16;
                          checkI32(val); // 7

                          val = val * val;
                          checkI32(val); // 49
                      }

                      fn checkI32(value: i32)
                      {}
                      """;
        IRInterpreter interpreter = MakeInterpreter(code);

        int curI32Pos = 0;
        var CheckI32 = (int a) => { _testOutputHelper.WriteLine(a.ToString()); };

        interpreter.OverrideFunction("checkI32", CheckI32);
        interpreter.RunFunction("main");
    }

    private IRInterpreter MakeInterpreter(string code)
    {
        Diagnostic diag = new();
        Frontend.Result frontendResult = Frontend.Run(code, diag, timers: null);
        Assert.False(diag.HasErrors, diag.PrettyString());

        IRTypeLowerer lowerer = new();
        IRGen gen = new(lowerer);
        IRModule module = gen.Run(frontendResult.HIRCompilationUnit);

        IRInterpreter interpreter = new(module);
        return interpreter;
    }
}
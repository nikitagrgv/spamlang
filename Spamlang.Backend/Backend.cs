using Spamlang.Frontend;

namespace Spamlang.Backend;

public class Backend
{
    public struct Result
    {
        public required IRModule IRModule { get; init; }
        public required MModule MModule { get; init; }
    }

    public static Result Run(HIRCompilationUnit compilationUnit, Timers? timers)
    {
        IRTypeLowerer lowerer = new();

        timers?.RestartTimer();
        IRGen irGen = new(lowerer);
        IRModule irModule = irGen.Run(compilationUnit);
        timers?.FinishTimer("IR");

        timers?.RestartTimer();
        Codegen codegen = new();
        MModule mmodule = codegen.Run(irModule);
        timers?.FinishTimer("MIR");

        return new Result
        {
            IRModule = irModule,
            MModule = mmodule,
        };
    }
}
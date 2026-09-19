using Spamlang.Frontend;

namespace Spamlang.Backend;

public class Backend
{
    public struct Result
    {
        public required IRModule IRModule { get; init; }
        public required MModule MModule { get; init; }
    }

    public static Result Run(HIRCompilationUnit compilationUnit, TypeRegistry typeRegistry, Timers? timers)
    {
        timers?.RestartTimer();
        IRGen irGen = new(typeRegistry);
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
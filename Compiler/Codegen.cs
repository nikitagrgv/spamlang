namespace Compiler;

public class Codegen
{
    public MModule Run(IRModule module)
    {
        List<MFunction> functions = new();

        MModule mmodule = new()
        {
            Functions = functions
        };
        return mmodule;
    }
}
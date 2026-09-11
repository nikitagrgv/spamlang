namespace Compiler;

public class Codegen
{
    public MModule Run(IRModule irmodule)
    {
        List<MFunction> functions = new();

        foreach (IRFunction func in irmodule.Functions)
        {
        }

        MModule module = new()
        {
            Functions = functions
        };
        return module;
    }

    public MFunction GenFunction(IRFunction irfunc)
    {
    }
}
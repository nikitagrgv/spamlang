namespace Compiler;

public class Codegen
{
    public MModule Run(IRModule irmodule)
    {
        List<MFunction> functions = new();

        foreach (IRFunction irfunc in irmodule.Functions)
        {
            MFunction func = GenFunction(irfunc);
            functions.Add(func);
        }

        MModule module = new()
        {
            Functions = functions
        };
        return module;
    }

    private MFunction GenFunction(IRFunction irfunc)
    {
        // TODO: Consider alignment!
        // TODO: Reuse lists
        // TODO: Don't spill params on stack?

        List<MBasicBlock> basicBlocks = new();

        int curOffset = 0;
        List<int> paramsOffsets = new();
        foreach (IRParam param in irfunc.Params)
        {
            curOffset += param.Type.Size;
            curOffset = AlignTo(curOffset, param.Type.)
            paramsOffsets.Add(curOffset);
        }

        MFunction func = new()
        {
            BasicBlocks = basicBlocks,
            Name = irfunc.Name
        };
        return func;
    }

    private int AlignTo(int offset, int alignment)
    {
        return (offset + alignment - 1) & ~(alignment - 1);
    }
}
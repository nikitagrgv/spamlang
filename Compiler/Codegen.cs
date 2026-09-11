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
        // TODO: Reuse lists
        // TODO: Don't spill params on stack?

        int curOffset = 0;
        List<int> paramsOffsets = new();
        foreach (IRParam param in irfunc.Params)
        {
            curOffset += param.Type.Size;
            curOffset = AlignTo(curOffset, param.Type.Alignment);
            paramsOffsets.Add(curOffset);
        }

        // Collect alloca-s
        List<MBasicBlock> basicBlocks = new();
        foreach (IRBasicBlock irbb in irfunc.BasicBlocks)
        {
            foreach (IRInstruction instr in irbb.Instructions)
            {
                if (instr is not IRInstructionAlloca { } alloca)
            }
        }


        MFunction func = new()
        {
            BasicBlocks = basicBlocks,
            Name = irfunc.Name
        };
        return func;
    }

    private static int AlignTo(int offset, int alignment)
    {
        return (offset + alignment - 1) & ~(alignment - 1);
    }
}
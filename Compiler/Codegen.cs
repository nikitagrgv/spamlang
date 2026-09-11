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

        // Spill params to stack
        int curOffset = 0;
        List<int> paramsOffsets = new();
        foreach (IRParam param in irfunc.Params)
        {
            curOffset += param.Type.Size;
            curOffset = AlignTo(curOffset, param.Type.Alignment);
            paramsOffsets.Add(curOffset);
        }

        // Allocate space for alloca instructions
        List<MBasicBlock> basicBlocks = new();
        List<int> allocatedOffsets = new();
        foreach (IRBasicBlock irbb in irfunc.BasicBlocks)
        {
            foreach (IRInstruction instr in irbb.Instructions)
            {
                if (instr is not IRInstructionAlloca { } alloca)
                {
                    continue;
                }

                curOffset += alloca.AllocatedType.Size;
                curOffset = AlignTo(curOffset, alloca.AllocatedType.Alignment);
                allocatedOffsets.Add(curOffset);
            }
        }

        // TODO: Shitty, make list
        Dictionary<int, int> instrIdToOffset = new();
        foreach (IRBasicBlock irbb in irfunc.BasicBlocks)
        {
            foreach (IRInstruction instr in irbb.Instructions)
            {
                curOffset += instr.Type.Size;
                curOffset = AlignTo(curOffset, instr.Type.Alignment);
                instrIdToOffset.Add(instr.Id, curOffset);
            }
        }

        // TODO: Do this only if calls exists!
        int frameSize = curOffset;
        frameSize += 0x20; // Shadow space
        frameSize = AlignTo(frameSize, 16); // ABI requirement

        // frame prologue


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
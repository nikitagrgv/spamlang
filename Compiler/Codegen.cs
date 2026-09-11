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
                switch (instr)
                {
                    case IRInstructionAlloca irInstructionAlloca:
                        break;
                    case IRInstructionBinary irInstructionBinary:
                        break;
                    case IRInstructionCall irInstructionCall:
                        break;
                    case IRInstructionCast irInstructionCast:
                        break;
                    case IRInstructionLoad irInstructionLoad:
                        break;
                    case IRInstructionRet irInstructionRet:
                        break;
                    case IRInstructionStore irInstructionStore:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(instr));
                }
            }
        }

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
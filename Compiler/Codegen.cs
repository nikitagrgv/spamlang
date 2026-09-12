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

        // Allocate spilled params to stack
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
        bool hasCalls = false;
        foreach (IRBasicBlock irbb in irfunc.BasicBlocks)
        {
            foreach (IRInstruction instr in irbb.Instructions)
            {
                if (!hasCalls && instr is IRInstructionCall)
                {
                    hasCalls = true;
                }

                if (instr is IRInstructionAlloca alloca)
                {
                    curOffset += alloca.AllocatedType.Size;
                    curOffset = AlignTo(curOffset, alloca.AllocatedType.Alignment);
                    allocatedOffsets.Add(curOffset);
                }
            }
        }

        // TODO: Shitty, make list
        Dictionary<int, int> instrIdToOffset = new();
        foreach (IRBasicBlock irbb in irfunc.BasicBlocks)
        {
            foreach (IRInstruction instr in irbb.Instructions)
            {
                // TODO: Shit?
                if (instr.Type.Size == 0)
                {
                    continue;
                }

                curOffset += instr.Type.Size;
                curOffset = AlignTo(curOffset, instr.Type.Alignment);
                instrIdToOffset.Add(instr.Id, curOffset);
            }
        }

        int frameSize = curOffset;
        if (hasCalls)
        {
            frameSize += 0x20; // Shadow space
            frameSize = AlignTo(frameSize, 16); // ABI requirement
        }

        // frame prologue
        List<MInstr> prologueInstructions = new();
        prologueInstructions.Add(new MInstr
        {
            Op = MOpcode.Push,
            Left = MOpReg.Rbp,
        });
        prologueInstructions.Add(new MInstr
        {
            Op = MOpcode.Mov,
            Left = MOpReg.Rbp,
            Right = MOpReg.Rsp,
        });
        prologueInstructions.Add(new MInstr
        {
            Op = MOpcode.Sub,
            Left = MOpReg.Rsp,
            Right = new MOpImm { Value = frameSize },
        });

        // Spill params to stack
        // TODO: Full ABI for params! stack/floats/structs
        // TODO: SystemV ABI
        Reg[] paramsRegs =
        [
            Reg.Rcx,
            Reg.Rdx,
            Reg.R8,
            Reg.R9,
        ];
        foreach (IRParam param in irfunc.Params)
        {
            if (param.Index >= paramsRegs.Length)
            {
                throw new NotImplementedException();
            }

            RegClass regClass;
            if (param.Type == BuiltinType.I32 || param.Type == BuiltinType.Ptr)
            {
                regClass = RegClass.Int;
            }
            else
            {
                throw new NotImplementedException();
            }

            if (regClass != RegClass.Int)
            {
                throw new NotImplementedException();
            }

            Reg paramReg = paramsRegs[param.Index];
            int paramOffset = paramsOffsets[param.Index];
            int typeSize = param.Type.Size;
            prologueInstructions.Add(new MInstr
            {
                Op = MOpcode.Mov,
                Left = new MOpMem
                {
                    Base = Reg.Rbp,
                    Offset = -paramOffset,
                    Size = typeSize
                },
                Right = new MOpReg
                {
                    Reg = paramReg,
                    Size = typeSize
                }
            });
        }

        MBasicBlock prologue = new()
        {
            Instructions = prologueInstructions,
            Name = null,
        };
        basicBlocks.Add(prologue);

        // blocks
        int curAlloca = 0;
        foreach (IRBasicBlock irbb in irfunc.BasicBlocks)
        {
            List<MInstr> instructions = new();

            foreach (IRInstruction instr in irbb.Instructions)
            {
                switch (instr)
                {
                    case IRInstructionAlloca alloca:
                    {
                        int allocatedOffset = allocatedOffsets[curAlloca];
                        int instrOffset = instrIdToOffset[instr.Id];
                        ++curAlloca;
                        instructions.Add(new MInstr
                        {
                            Op = MOpcode.Lea,
                            Left = MOpReg.Rax,
                            Right = new MOpMem
                            {
                                Base = Reg.Rbx,
                                Offset = -allocatedOffset,
                                Size = 8
                            },
                            Comment = alloca.PrintDefinition()
                        });
                        instructions.Add(new MInstr
                        {
                            Op = MOpcode.Mov,
                            Left = new MOpMem
                            {
                                Base = Reg.Rbp,
                                Offset = -instrOffset,
                                Size = 8,
                            },
                            Right = MOpReg.Rax,
                        });
                        break;
                    }
                    case IRInstructionBinary binary:
                    {
                        int leftId = binary.Left.Id;
                        int rightId = binary.Right.Id;
                        int instrId = binary.Id;
                        int leftOffset = instrIdToOffset[leftId];
                        int rightOffset = instrIdToOffset[rightId];
                        int instrOffset = instrIdToOffset[instrId];
                        switch (binary.Op)
                        {
                            case IRBinaryOp.Add:
                            {
                                break;
                            }
                            case IRBinaryOp.Sub:
                            {
                                break;
                            }
                            case IRBinaryOp.Mul:
                            {
                                break;
                            }
                            case IRBinaryOp.SDiv:
                            {
                                throw new NotImplementedException();
                            }
                            case IRBinaryOp.UDiv:
                            {
                                throw new NotImplementedException();
                            }
                            case IRBinaryOp.SRem:
                            {
                                throw new NotImplementedException();
                            }
                            case IRBinaryOp.URem:
                            {
                                throw new NotImplementedException();
                            }
                            default:
                            {
                                throw new ArgumentOutOfRangeException();
                            }
                        }

                        break;
                    }
                    case IRInstructionCall call:
                        break;
                    case IRInstructionLoad load:
                        break;
                    case IRInstructionRet ret:
                        break;
                    case IRInstructionStore store:
                        break;
                    case IRInstructionCast cast:
                        throw new NotImplementedException();
                    default:
                        throw new ArgumentOutOfRangeException(nameof(instr));
                }
            }

            MBasicBlock block = new()
            {
                Instructions = instructions,
                Name = $".L{irfunc.Name}_{irbb.Name}",
            };
            basicBlocks.Add(block);
        }

        // frame epilogue
        List<MInstr> epilogueInstructions = new();
        epilogueInstructions.Add(new MInstr
        {
            Op = MOpcode.Mov,
            Left = MOpReg.Rsp,
            Right = MOpReg.Rbp,
        });
        epilogueInstructions.Add(new MInstr
        {
            Op = MOpcode.Pop,
            Left = MOpReg.Rbp,
        });
        epilogueInstructions.Add(new MInstr
        {
            Op = MOpcode.Ret,
        });
        MBasicBlock epilogue = new()
        {
            Instructions = epilogueInstructions,
            Name = $".L{irfunc.Name}_epi",
        };
        basicBlocks.Add(epilogue);


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
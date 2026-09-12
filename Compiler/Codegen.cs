using System.Diagnostics;

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
            Functions = functions,
        };
        return module;
    }

    // TODO: Too many allocations!
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

        MOpReg rbp = new() { Reg = Reg.Rbp, Size = 8 };
        MOpReg rsp = new() { Reg = Reg.Rsp, Size = 8 };

        // frame prologue
        List<MInstr> prologueInstructions = new();
        prologueInstructions.Add(new MInstr
        {
            Op = MOpcode.Push,
            Left = rbp,
        });
        prologueInstructions.Add(new MInstr
        {
            Op = MOpcode.Mov,
            Left = rbp,
            Right = rsp,
        });
        prologueInstructions.Add(new MInstr
        {
            Op = MOpcode.Sub,
            Left = rsp,
            Right = new MOpImm { Value = frameSize },
        });

        MOperand ToOperand(IRValue value)
        {
            switch (value)
            {
                case IRConstantInt v:
                {
                    MOpImm op = new() { Value = v.Value };
                    return op;
                }
                case IRFunction v:
                {
                    MOpLabel op = new() { Label = v.Name };
                    return op;
                }
                case IRInstruction v:
                {
                    int instrOffset = instrIdToOffset[v.Id];
                    int typeSize = v.Type.Size;
                    MOpMem op = new()
                    {
                        Base = Reg.Rbp,
                        Offset = -instrOffset,
                        Size = typeSize,
                    };
                    return op;
                }
                case IRParam v:
                {
                    int paramOffset = paramsOffsets[v.Index];
                    int typeSize = v.Type.Size;
                    MOpMem op = new()
                    {
                        Base = Reg.Rbp,
                        Offset = -paramOffset,
                        Size = typeSize,
                    };
                    return op;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
            }
        }

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
            int typeSize = param.Type.Size;
            MOperand paramOperand = ToOperand(param);
            prologueInstructions.Add(new MInstr
            {
                Op = MOpcode.Mov,
                Left = paramOperand,
                Right = new MOpReg
                {
                    Reg = paramReg,
                    Size = typeSize,
                },
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
        string epilogueLabel = $".L{irfunc.Name}_epi";
        foreach (IRBasicBlock irbb in irfunc.BasicBlocks)
        {
            List<MInstr> instructions = new();

            foreach (IRInstruction instr in irbb.Instructions)
            {
                switch (instr)
                {
                    case IRInstructionAlloca alloca:
                    {
                        // TODO: Use lea?
                        int allocatedOffset = allocatedOffsets[curAlloca];
                        int instrOffset = instrIdToOffset[instr.Id];
                        ++curAlloca;
                        instructions.Add(new MInstr
                        {
                            Op = MOpcode.Lea,
                            Left = new MOpReg
                            {
                                Reg = Reg.Rax,
                                Size = alloca.Type.Size,
                            },
                            Right = new MOpMem
                            {
                                Base = Reg.Rbp,
                                Offset = -allocatedOffset,
                                Size = null,
                            },
                            Comment = instr.PrintDefinition(),
                        });
                        instructions.Add(new MInstr
                        {
                            Op = MOpcode.Mov,
                            Left = new MOpMem
                            {
                                Base = Reg.Rbp,
                                Offset = -instrOffset,
                                Size = alloca.Type.Size,
                            },
                            Right = new MOpReg
                            {
                                Reg = Reg.Rax,
                                Size = alloca.Type.Size,
                            },
                        });
                        break;
                    }
                    case IRInstructionBinary binary:
                    {
                        Debug.Assert(binary.Left.Type == binary.Right.Type);
                        int typeSize = binary.Type.Size;
                        int instrOffset = instrIdToOffset[binary.Id];
                        switch (binary.Op)
                        {
                            case IRBinaryOp.Add:
                            case IRBinaryOp.Sub:
                            {
                                MOpReg leftReg = typedRax;
                                MOpReg rightReg = typedRcx;
                                instructions.Add(new MInstr
                                {
                                    Op = MOpcode.Mov,
                                    Left = leftReg,
                                    Right = ToOperand(binary.Left),
                                    Comment = instr.PrintDefinition(),
                                });
                                instructions.Add(new MInstr
                                {
                                    Op = MOpcode.Mov,
                                    Left = rightReg,
                                    Right = ToOperand(binary.Right),
                                });
                                instructions.Add(new MInstr
                                {
                                    Op = binary.Op == IRBinaryOp.Add ? MOpcode.Add : MOpcode.Sub,
                                    Left = leftReg,
                                    Right = rightReg,
                                });
                                instructions.Add(new MInstr
                                {
                                    Op = MOpcode.Mov,
                                    Left = leftReg,
                                    Right = new MOpMem
                                    {
                                        Base = Reg.Rbp,
                                        Offset = -instrOffset,
                                        Size = typeSize,
                                    },
                                });
                                break;
                            }
                            case IRBinaryOp.Mul:
                            {
                                // TODO: Copy pasted from add/sub
                                MOpReg leftReg = typedRax;
                                MOpReg rightReg = typedRcx;
                                instructions.Add(new MInstr
                                {
                                    Op = MOpcode.Mov,
                                    Left = leftReg,
                                    Right = ToOperand(binary.Left),
                                    Comment = instr.PrintDefinition(),
                                });
                                instructions.Add(new MInstr
                                {
                                    Op = MOpcode.Mov,
                                    Left = rightReg,
                                    Right = ToOperand(binary.Right),
                                });
                                instructions.Add(new MInstr
                                {
                                    Op = MOpcode.Imul,
                                    Left = leftReg,
                                    Right = rightReg,
                                });
                                instructions.Add(new MInstr
                                {
                                    Op = MOpcode.Mov,
                                    Left = leftReg,
                                    Right = new MOpMem
                                    {
                                        Base = Reg.Rbp,
                                        Offset = -instrOffset,
                                        Size = typeSize,
                                    },
                                });
                                break;
                            }
                            case IRBinaryOp.SDiv:
                            case IRBinaryOp.UDiv:
                            case IRBinaryOp.SRem:
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
                    {
                        for (int i = 0; i < call.Args.Count; i++)
                        {
                            IRValue arg = call.Args[i];
                            Reg argReg = paramsRegs[i];
                            int typeSize = arg.Type.Size;
                            string? comment = null;
                            if (i == 0)
                            {
                                comment = instr.PrintDefinition();
                            }

                            instructions.Add(new MInstr
                            {
                                Op = MOpcode.Mov,
                                Left = new MOpReg { Reg = argReg, Size = typeSize },
                                Right = ToOperand(arg),
                                Comment = comment,
                            });
                        }

                        instructions.Add(new MInstr
                        {
                            Op = MOpcode.Call,
                            Left = ToOperand(call.Callee),
                        });

                        // TODO: Correct ABI! SRet (in IR maybe)
                        if (call.Type.Size > 0)
                        {
                            if (call.Type.Size > 8 || !IsPowerOrTwo(call.Type.Size))
                            {
                                throw new NotImplementedException();
                            }

                            int instrOffset = instrIdToOffset[call.Id];
                            instructions.Add(new MInstr
                            {
                                Op = MOpcode.Mov,
                                Left = new MOpMem
                                {
                                    Base = Reg.Rbp,
                                    Offset = -instrOffset,
                                    Size = call.Type.Size,
                                },
                                Right = typedRax,
                            });
                        }

                        break;
                    }
                    case IRInstructionLoad load:
                        break;
                    case IRInstructionRet ret:
                    {
                        if (ret.Value != null)
                        {
                            // TODO: Correct ABI! SRet (in IR maybe)
                            instructions.Add(new MInstr
                            {
                                Op = MOpcode.Mov,
                                Left = typedRax,
                                Right = ToOperand(ret.Value),
                                Comment = instr.PrintDefinition(),
                            });
                        }

                        instructions.Add(new MInstr
                        {
                            Op = MOpcode.Jmp,
                            Left = new MOpLabel { Label = epilogueLabel },
                        });

                        break;
                    }
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
            Left = rsp,
            Right = rbp,
        });
        epilogueInstructions.Add(new MInstr
        {
            Op = MOpcode.Pop,
            Left = rbp,
        });
        epilogueInstructions.Add(new MInstr
        {
            Op = MOpcode.Ret,
        });
        MBasicBlock epilogue = new()
        {
            Instructions = epilogueInstructions,
            Name = epilogueLabel,
        };
        basicBlocks.Add(epilogue);


        MFunction func = new()
        {
            BasicBlocks = basicBlocks,
            Name = irfunc.Name,
        };
        return func;
    }

    private static bool IsPowerOrTwo(int n)
    {
        return n > 0 && (n & (n - 1)) == 0;
    }

    private static int AlignTo(int offset, int alignment)
    {
        return (offset + alignment - 1) & ~(alignment - 1);
    }
}
using System.Diagnostics;

namespace Spamlang;

public class Codegen
{
    private struct FuncContext
    {
        public required List<int> ParamsOffsets { get; init; }

        public required List<int> AllocatedOffsets { get; init; }

        // TODO: Use list?
        public required Dictionary<int, int> InstrIdToOffset { get; init; }
    }

    private Dictionary<FuncType, FuncABI> _funcTypeToAbi = new();
    private FuncABIGen _funcABIGen = new();

    public MModule Run(IRModule irmodule)
    {
        List<MFunction> functions = new();

        foreach (IRFunction irfunc in irmodule.Functions)
        {
            MFunction func = GenFunction(irfunc);
            functions.Add(func);
        }

        // We only export main now
        List<string> exports = ["main"];
        MModule module = new()
        {
            Functions = functions,
            Exports = exports,
        };
        return module;
    }

    // TODO: Too many allocations!
    // TODO: Reuse lists
    // TODO: Don't spill params on stack?
    private MFunction GenFunction(IRFunction irfunc)
    {
        FuncABI funcABI = GetFuncABI(irfunc.LoweredSignature);
        FuncContext ctx = new()
        {
            ParamsOffsets = new List<int>(),
            AllocatedOffsets = new List<int>(),
            InstrIdToOffset = new Dictionary<int, int>(),
        };

        // Allocate spilled params to stack
        int curOffset = 0;
        foreach (IRParam param in irfunc.Params)
        {
            curOffset += param.Type.Size;
            curOffset = AlignTo(curOffset, param.Type.Alignment);
            ctx.ParamsOffsets.Add(-curOffset);
        }

        // Allocate space for alloca instructions
        List<MBasicBlock> basicBlocks = new();
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
                    ctx.AllocatedOffsets.Add(-curOffset);
                }
            }
        }

        // Allocate space for every instruction
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
                ctx.InstrIdToOffset.Add(instr.Id, -curOffset);
            }
        }

        int frameSize = curOffset;
        if (hasCalls)
        {
            frameSize += 0x20; // Shadow space for functions calls (Windows ABI), NOT LINUX!
        }

        frameSize = AlignTo(frameSize, 16); // For functions calls ABI and SSE

        // Frame prologue
        MOpReg rbp = new() { Reg = Reg.Rbp, Size = 8 };
        MOpReg rsp = new() { Reg = Reg.Rsp, Size = 8 };
        List<MInstr> prologueInstructions = new();
        prologueInstructions.Add(new MInstr { Op = MOpcode.Push, Left = rbp, });
        prologueInstructions.Add(new MInstr { Op = MOpcode.Mov, Left = rbp, Right = rsp, });
        prologueInstructions.Add(new MInstr { Op = MOpcode.Sub, Left = rsp, Right = new MOpImm { Value = frameSize }, });

        // Spill params to stack
        foreach (IRParam param in irfunc.Params)
        {
            int index = param.Index;
            int paramOffset = ctx.ParamsOffsets[index];
            int typeSize = param.Type.Size;

            MValueLocation loc = funcABI.ParamLocations[index];
            switch (loc)
            {
                case MValueLocationRegister regLoc:
                    StoreToOffset(ctx, prologueInstructions, regLoc.Reg, paramOffset, typeSize);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        MBasicBlock prologue = new()
        {
            Instructions = prologueInstructions,
            Name = null,
        };
        basicBlocks.Add(prologue);

        // Generate code
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
                        GenAlloca(ctx, instructions, alloca, curAlloca);
                        curAlloca++;
                        break;
                    case IRInstructionBinary binary:
                        GenBinary(ctx, instructions, binary);
                        break;
                    case IRInstructionCall call:
                        FuncABI abi = GetFuncABI(call.LoweredSignature);
                        GenCall(ctx, abi, instructions, call);
                        break;
                    case IRInstructionRet ret:
                        GenRet(ctx, funcABI, instructions, ret, epilogueLabel);
                        break;
                    case IRInstructionStore store:
                        GenStore(ctx, instructions, store);
                        break;
                    case IRInstructionLoad load:
                        GenLoad(ctx, instructions, load);
                        break;
                    case IRInstructionCast cast:
                        GenCast(ctx, instructions, cast);
                        break;
                    default:
                    {
                        throw new ArgumentOutOfRangeException(nameof(instr));
                    }
                }
            }

            MBasicBlock block = new()
            {
                Instructions = instructions,
                Name = $".L{irfunc.Name}_{irbb.Name}",
            };
            basicBlocks.Add(block);
        }

        // Frame epilogue
        List<MInstr> epilogueInstructions = new();
        epilogueInstructions.Add(new MInstr { Op = MOpcode.Mov, Left = rsp, Right = rbp, });
        epilogueInstructions.Add(new MInstr { Op = MOpcode.Pop, Left = rbp, });
        epilogueInstructions.Add(new MInstr { Op = MOpcode.Ret, });
        MBasicBlock epilogue = new() { Instructions = epilogueInstructions, Name = epilogueLabel, };
        basicBlocks.Add(epilogue);

        MFunction func = new()
        {
            BasicBlocks = basicBlocks,
            Name = irfunc.Name,
        };
        return func;
    }

    private static void GenAlloca(FuncContext ctx, List<MInstr> instructions, IRInstructionAlloca instr, int curAlloca)
    {
        int allocatedOffset = ctx.AllocatedOffsets[curAlloca];
        int instrOffset = ctx.InstrIdToOffset[instr.Id];
        instructions.Add(new MInstr
        {
            Op = MOpcode.Lea,
            Left = new MOpReg { Reg = Reg.Rax, Size = instr.Type.Size, },
            Right = new MOpMem { Base = Reg.Rbp, Offset = allocatedOffset, Size = null },
            Comment = instr.PrintDefinition(),
        });
        StoreToOffset(ctx, instructions, Reg.Rax, instrOffset, instr.Type.Size);
    }

    private static void GenBinary(FuncContext ctx, List<MInstr> instructions, IRInstructionBinary instr)
    {
        Debug.Assert(instr.Left.Type == instr.Right.Type);
        int typeSize = instr.Type.Size;
        int instrOffset = ctx.InstrIdToOffset[instr.Id];
        switch (instr.Op)
        {
            case IRBinaryOp.Add:
            case IRBinaryOp.Sub:
            {
                LoadToReg(ctx, instructions, instr.Left, Reg.Rax, instr.PrintDefinition());
                LoadToReg(ctx, instructions, instr.Right, Reg.Rcx);
                instructions.Add(new MInstr
                {
                    Op = instr.Op == IRBinaryOp.Add ? MOpcode.Add : MOpcode.Sub,
                    Left = new MOpReg { Reg = Reg.Rax, Size = typeSize },
                    Right = new MOpReg { Reg = Reg.Rcx, Size = typeSize },
                });
                StoreToOffset(ctx, instructions, Reg.Rax, instrOffset, typeSize);
                break;
            }
            case IRBinaryOp.Mul:
            {
                LoadToReg(ctx, instructions, instr.Left, Reg.Rax, instr.PrintDefinition());
                LoadToReg(ctx, instructions, instr.Right, Reg.Rcx);
                instructions.Add(new MInstr
                {
                    Op = MOpcode.Imul,
                    Left = new MOpReg { Reg = Reg.Rax, Size = typeSize },
                    Right = new MOpReg { Reg = Reg.Rcx, Size = typeSize },
                });
                StoreToOffset(ctx, instructions, Reg.Rax, instrOffset, typeSize);
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
    }

    private static void GenCall(FuncContext ctx, FuncABI abi, List<MInstr> instructions, IRInstructionCall instr)
    {
        string? comment = instr.PrintDefinition();
        for (int i = 0; i < instr.Args.Count; i++)
        {
            IRValue arg = instr.Args[i];
            MValueLocation paramLoc = abi.ParamLocations[i];
            switch (paramLoc)
            {
                case MValueLocationRegister regLoc:
                    LoadToReg(ctx, instructions, arg, regLoc.Reg, comment);
                    comment = null;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        EmitCall(ctx, instructions, instr.Callee, comment);

        Type returnType = instr.Type;
        if (returnType == BuiltinType.Void)
        {
            return;
        }

        int dstOffset = ctx.InstrIdToOffset[instr.Id];

        Debug.Assert(abi.ReturnLocation != null);
        MValueLocation retLoc = abi.ReturnLocation;
        switch (retLoc)
        {
            case MValueLocationRegister regLoc:
                StoreToOffset(ctx, instructions, regLoc.Reg, dstOffset, returnType.Size);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    private static void GenRet(FuncContext ctx, FuncABI abi, List<MInstr> instructions, IRInstructionRet instr, string epilogueLabel)
    {
        string? comment = instr.PrintDefinition();
        if (instr.Value != null)
        {
            Debug.Assert(abi.ReturnLocation != null);
            MValueLocation retLoc = abi.ReturnLocation;
            switch (retLoc)
            {
                case MValueLocationRegister regLoc:
                    LoadToReg(ctx, instructions, instr.Value, regLoc.Reg, comment);
                    break;
                default:
                    throw new NotImplementedException();
            }

            comment = null;
        }

        instructions.Add(new MInstr
        {
            Op = MOpcode.Jmp,
            Left = new MOpLabel { Label = epilogueLabel },
            Comment = comment,
        });
    }

    private static void GenStore(FuncContext ctx, List<MInstr> instructions, IRInstructionStore instr)
    {
        LoadToReg(ctx, instructions, instr.Address, Reg.Rcx, instr.PrintDefinition());
        LoadToReg(ctx, instructions, instr.Value, Reg.Rax);
        // Store: [RCX] <- RAX
        int typeSize = instr.Value.Type.Size;
        instructions.Add(new MInstr
        {
            Op = MOpcode.Mov,
            Left = new MOpMem { Base = Reg.Rcx, Offset = null, Size = typeSize, },
            Right = new MOpReg { Reg = Reg.Rax, Size = typeSize, },
        });
    }

    private static void GenLoad(FuncContext ctx, List<MInstr> instructions, IRInstructionLoad instr)
    {
        LoadToReg(ctx, instructions, instr.Address, Reg.Rcx, instr.PrintDefinition());
        // Load: RAX <- [RCX]
        instructions.Add(new MInstr
        {
            Op = MOpcode.Mov,
            Left = new MOpReg { Reg = Reg.Rax, Size = instr.Type.Size, },
            Right = new MOpMem { Base = Reg.Rcx, Offset = null, Size = instr.Type.Size, },
        });
        int instrOffset = ctx.InstrIdToOffset[instr.Id];
        StoreToOffset(ctx, instructions, Reg.Rax, instrOffset, instr.Type.Size);
    }

    private static void GenCast(FuncContext ctx, List<MInstr> instructions, IRInstructionCast instr)
    {
        throw new NotImplementedException();
    }

    private static MOpMem ParamToOperand(FuncContext ctx, IRParam param)
    {
        int paramOffset = ctx.ParamsOffsets[param.Index];
        int typeSize = param.Type.Size;
        MOpMem op = new()
        {
            Base = Reg.Rbp,
            Offset = paramOffset,
            Size = typeSize,
        };
        return op;
    }

    private static MOpMem InstructionToOperand(FuncContext ctx, IRInstruction instr)
    {
        int instrOffset = ctx.InstrIdToOffset[instr.Id];
        int typeSize = instr.Type.Size;
        MOpMem op = new()
        {
            Base = Reg.Rbp,
            Offset = instrOffset,
            Size = typeSize,
        };
        return op;
    }

    private static void LoadToReg(FuncContext ctx, List<MInstr> instructions, IRValue value, Reg reg, string? comment = null)
    {
        switch (value)
        {
            case IRInstruction v:
            {
                MOperand op = InstructionToOperand(ctx, v);
                instructions.Add(new MInstr
                {
                    Op = MOpcode.Mov,
                    Left = new MOpReg { Reg = reg, Size = value.Type.Size },
                    Right = op,
                    Comment = comment,
                });
                break;
            }
            case IRParam v:
            {
                MOperand op = ParamToOperand(ctx, v);
                instructions.Add(new MInstr
                {
                    Op = MOpcode.Mov,
                    Left = new MOpReg { Reg = reg, Size = value.Type.Size },
                    Right = op,
                    Comment = comment,
                });
                break;
            }
            case IRConstantInt v:
            {
                if (v.Type.Size > 8)
                {
                    throw new NotImplementedException();
                }

                instructions.Add(new MInstr
                {
                    Op = MOpcode.Mov,
                    Left = new MOpReg { Reg = reg, Size = value.Type.Size },
                    Right = new MOpImm { Value = v.Value },
                    Comment = comment,
                });

                break;
            }
            case IRFunction v:
            {
                instructions.Add(new MInstr
                {
                    Op = MOpcode.Lea,
                    Left = new MOpReg { Reg = reg, Size = value.Type.Size },
                    Right = new MOpMemLabel
                    {
                        Base = Reg.Rip,
                        Label = v.Name,
                        Size = null,
                    },
                    Comment = comment,
                });
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    private static void StoreToOffset(FuncContext ctx, List<MInstr> instructions, Reg srcReg, int offset, int size, string? comment = null)
    {
        instructions.Add(new MInstr
        {
            Op = MOpcode.Mov,
            Left = new MOpMem
            {
                Base = Reg.Rbp,
                Offset = offset,
                Size = size,
            },
            Right = new MOpReg { Reg = srcReg, Size = size },
            Comment = comment,
        });
    }

    private static void EmitCall(FuncContext ctx, List<MInstr> instructions, IRValue callee, string? comment = null)
    {
        Debug.Assert(callee is not IRConstantInt, "Can't call immediate value");

        switch (callee)
        {
            case IRFunction v:
            {
                instructions.Add(new MInstr
                {
                    Op = MOpcode.Call,
                    Left = new MOpLabel { Label = v.Name },
                    Comment = comment,
                });
                break;
            }
            case IRInstruction v:
            {
                MOperand op = InstructionToOperand(ctx, v);
                instructions.Add(new MInstr
                {
                    Op = MOpcode.Call,
                    Left = op,
                    Comment = comment,
                });
                break;
            }
            case IRParam v:
            {
                MOperand op = ParamToOperand(ctx, v);
                instructions.Add(new MInstr
                {
                    Op = MOpcode.Call,
                    Left = op,
                    Comment = comment,
                });
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(callee));
        }
    }

    private FuncABI GetFuncABI(FuncType funcType)
    {
        if (_funcTypeToAbi.TryGetValue(funcType, out FuncABI? abi))
        {
            return abi;
        }

        abi = _funcABIGen.Generate(funcType);
        _funcTypeToAbi[funcType] = abi;
        return abi;
    }

    private static int AlignTo(int offset, int alignment)
    {
        Debug.Assert(offset >= 0);
        return (offset + alignment - 1) & ~(alignment - 1);
    }
}
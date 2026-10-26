using System.Diagnostics;

namespace Spamlang.Backend;

public class IRInterpreterException : Exception
{
    public IRInterpreterException(string message) : base(message)
    {
    }
}

public class IRFunctionNotFoundException : IRInterpreterException
{
    public IRFunctionNotFoundException(string name) : base($"Function {name} not found")
    {
    }
}

public class IRUnexpectedSignatureException : IRInterpreterException
{
    public IRUnexpectedSignatureException(IRFunction func) : base($"Unexpected signature for {func.Name}. Expected {func.LoweredSignature}")
    {
    }
}

public class IRInvalidException : IRInterpreterException
{
    public IRInvalidException(string message) : base(message)
    {
    }
}

public class IRInterpreter
{
    private IRModule _module;

    // Memory layout:
    // 0x0000000000000000...0x8000000000000000 - Skipped. For catching nullptr dereferences and bad addresses.
    // 0x8000000000000000...0xFFFFFFFFFFFFFFFF - _memory. For alloca-s
    // Memory is shared between all frames.
    // Frames are separated by gaps of 256 bytes filled with 0xCD - for catching OOB accesses.

    private const ulong MemoryStartAddress = 0x8000000000000000;

    private const int FrameGapSize = 256;
    private const int AllocaGapSize = 32;

    private const byte GapTag = 0xED;
    private const byte UninitializedTag = 0xCD;

    private List<byte> _memory = new();
    private List<int> _frames = new();

    private Dictionary<ulong, Delegate> _funcOverrides = new();

    public IRInterpreter(IRModule module)
    {
        Debug.Assert(BitConverter.IsLittleEndian, "Only little endian is supported");

        _module = module;
    }

    public void OverrideFunction(string name, Delegate impl)
    {
        IRFunction? func = FindFunction(name);
        if (func == null)
        {
            throw new IRFunctionNotFoundException(name);
        }

        // TODO#
    }

    // TODO: Support calling functions with arguments and return values
    public void RunFunction(string name)
    {
        IRFunction? func = FindFunction(name);
        if (func == null)
        {
            throw new IRFunctionNotFoundException(name);
        }

        if (func.LoweredSignature.ParamTypes.Count != 0)
        {
            throw new IRUnexpectedSignatureException(func);
        }

        // TODO#
    }

    private IRInterpreterValue? RunFunction(IRFunction func, List<IRInterpreterValue> args)
    {
        if (func.BasicBlocks.Count == 0)
        {
            throw new IRInvalidException("Function has no basic blocks");
        }

        if (func.Params.Count != args.Count)
        {
            throw new IRInvalidException($"Expected {func.Params.Count} arguments, got {args.Count}");
        }

        Dictionary<int, IRInterpreterValue> idToValue = new();
        for (int i = 0; i < func.Params.Count; i++)
        {
            IRParam param = func.Params[i];
            IRInterpreterValue value = args[i];
            idToValue.Add(param.Id, value);
        }

        bool exit = false;
        IRInterpreterValue? returnValue = null;
        foreach (IRBasicBlock block in func.BasicBlocks)
        {
            if (exit)
            {
                break;
            }

            foreach (IRInstruction instr in block.Instructions)
            {
                if (exit)
                {
                    break;
                }

                switch (instr)
                {
                    case IRInstructionAlloca alloca:
                        IRType type = alloca.AllocatedType;
                        ulong addr = Allocate(type.Size, type.Alignment);
                        break;
                    case IRInstructionLoad load:
                        break;
                    case IRInstructionStore store:
                        break;
                    case IRInstructionUnary unary:
                        break;
                    case IRInstructionBinary binary:
                        break;
                    case IRInstructionCall call:
                        break;
                    case IRInstructionCast cast:
                        break;
                    case IRInstructionCompare compare:
                        break;
                    case IRInstructionRet ret:
                        exit = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(instr));
                }
            }
        }

        return returnValue;
    }

    private void PushFrame()
    {
        int curSize = _memory.Count;
        _frames.Add(curSize);

        curSize += FrameGapSize;
        _memory.AddRange(Enumerable.Repeat(GapTag, FrameGapSize));
    }

    private void PopFrame()
    {
        Debug.Assert(_frames.Count > 0);
        Debug.Assert(_frames[^1] > _memory.Count);

        int prevSize = _frames[^1];
        _frames.RemoveAt(_frames.Count - 1);
        _memory.RemoveRange(prevSize, _memory.Count - prevSize);
    }

    private ulong Allocate(int size, int align)
    {
        int offset = _memory.Count;
        offset += AllocaGapSize;
        offset = Utils.AlignUp(offset, align);

        // Gap + align
        _memory.AddRange(Enumerable.Repeat(GapTag, offset - _memory.Count));

        // Allocate
        Debug.Assert(_memory.Count == offset);
        Debug.Assert(Utils.IsAligned(_memory.Count, align));
        _memory.AddRange(Enumerable.Repeat(UninitializedTag, size));

        return MemoryStartAddress + (ulong)offset;
    }

    private IRFunction? FindFunction(string name)
    {
        foreach (IRFunction func in _module.Functions)
        {
            if (func.Name == name)
            {
                return func;
            }
        }

        return null;
    }
}
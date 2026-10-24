using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

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

    private Dictionary<ulong, Delegate> _funcOverrides = new();

    private const ulong AllocatedStartAddress = 0x8000000000000000;
    private List<byte> _allocated = new();

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

                // TODO: Handle jumps
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

    private ulong Allocate(int size, int align)
    {
        
        Utils.AlignUp();
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
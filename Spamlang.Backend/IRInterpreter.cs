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

public struct IRInterpreterValue
{
    const int NumInlineBytes = 8;

    private IRType _type;
    private unsafe fixed byte _inlineBytes[NumInlineBytes];
    private byte[]? _heapBytes = null;
    private int _size = 0;

    public IRInterpreterValue(IRType type, ReadOnlySpan<byte> bytes)
    {
        _type = type;
        _size = bytes.Length;

        if (bytes.Length > 8)
        {
            _heapBytes = new byte[bytes.Length];
            bytes.CopyTo(_heapBytes);
            return;
        }

        Span<byte> inline = GetInlineBytes(bytes.Length);
        bytes.CopyTo(inline);
    }

    [UnscopedRef]
    public ReadOnlySpan<byte> AsBytes()
    {
        if (_heapBytes != null)
        {
            Debug.Assert(_heapBytes.Length == _size);
            return _heapBytes;
        }

        Span<byte> inline = GetInlineBytes(_size);
        return inline;
    }

    [UnscopedRef]
    private unsafe Span<byte> GetInlineBytes(int size)
    {
        Debug.Assert(size > 0 && size <= NumInlineBytes);
        return MemoryMarshal.CreateSpan<byte>(ref _inlineBytes[0], size);
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

    private IRInterpreterValue RunFunction(IRFunction func, List<IRInterpreterValue> args)
    {
        if (func.BasicBlocks.Count == 0)
        {
            throw new IRInvalidException("Function has no basic blocks");
        }

        IRBasicBlock entry = func.BasicBlocks[0];
        if (entry.Name != "entry")
        {
            throw new IRInvalidException("Function entry block is not named 'entry'");
        }

        foreach (IRInstruction instr in entry.Instructions)
        {
        }
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
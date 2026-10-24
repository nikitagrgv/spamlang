using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spamlang.Backend;

public struct IRInterpreterValue
{
    const int NumInlineBytes = 8;

    private readonly IRType _type;
    private unsafe fixed byte _inlineBytes[NumInlineBytes];
    private readonly byte[]? _heapBytes = null;
    private readonly int _size = 0;

    public IRType Type => _type;

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

    public UInt64 AsUInt64()
    {
        Debug.Assert(_size == 8);
        return BitConverter.ToUInt64(AsBytes());
    }

    public UInt32 AsUInt32()
    {
        Debug.Assert(_size == 4);
        return BitConverter.ToUInt32(AsBytes());
    }

    public UInt16 AsUInt16()
    {
        Debug.Assert(_size == 2);
        return BitConverter.ToUInt16(AsBytes());
    }

    public byte AsUInt8()
    {
        Debug.Assert(_size == 1);
        return AsBytes()[0];
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
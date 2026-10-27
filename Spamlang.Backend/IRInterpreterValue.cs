using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Spamlang.Backend;

public readonly struct IRInterpreterValue
{
    private readonly ulong _inline;
    private readonly ulong[]? _heap = null;
    private readonly int _size;

    public static readonly IRInterpreterValue Empty = new();

    public IRInterpreterValue()
    {
        _inline = 0;
        _size = 0;
    }

    public IRInterpreterValue(ReadOnlySpan<byte> bytes)
    {
        Debug.Assert(BitConverter.IsLittleEndian, "Only little endian is supported");
        _size = bytes.Length;

        if (bytes.Length > 8)
        {
            _heap = new ulong[(bytes.Length + 7) >> 3];
            bytes.CopyTo(MemoryMarshal.AsBytes(_heap.AsSpan()));
            return;
        }

        bytes.CopyTo(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref _inline, 1)));
    }

    public IRInterpreterValue(ulong value, int size)
    {
        Debug.Assert(BitConverter.IsLittleEndian, "Only little endian is supported");
        Debug.Assert(size > 0 && size <= 8);
        Debug.Assert(size == 8 || value < 1ul << (size * 8));
        _size = size;
        _inline = value;
    }

    public static IRInterpreterValue FromDouble(double value)
    {
        ulong v = BitConverter.DoubleToUInt64Bits(value);
        return new IRInterpreterValue(v, 8);
    }

    public static IRInterpreterValue FromFloat(float value)
    {
        ulong v = BitConverter.SingleToUInt32Bits(value);
        return new IRInterpreterValue(v, 4);
    }

    public UInt64 AsUInt64() => GetWord();
    public UInt32 AsUInt32() => (UInt32)GetWord();
    public UInt16 AsUInt16() => (UInt16)GetWord();
    public byte AsUInt8() => (byte)GetWord();

    public Int64 AsInt64() => unchecked((Int64)GetWord());
    public Int32 AsInt32() => unchecked((Int32)GetWord());
    public Int16 AsInt16() => unchecked((Int16)GetWord());
    public byte AsInt8() => unchecked((byte)GetWord());

    public double AsDouble() => BitConverter.UInt64BitsToDouble(GetWord());
    public float AsFloat() => BitConverter.UInt32BitsToSingle(AsUInt32());

    [UnscopedRef]
    public ReadOnlySpan<byte> AsBytes() => _heap is null
        ? MemoryMarshal.AsBytes(new ReadOnlySpan<ulong>(in _inline)).Slice(0, _size)
        : MemoryMarshal.AsBytes(_heap.AsSpan()).Slice(0, _size);

    private ulong GetWord()
    {
        return _heap is null ? _inline : _heap[0];
    }
}
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spamlang.Backend;

public struct IRInterpreterValue
{
    private ulong _inline;
    private readonly ulong[]? _heap = null;
    private readonly int _size = 0;

    public IRInterpreterValue(ReadOnlySpan<byte> bytes)
    {
        Debug.Assert(BitConverter.IsLittleEndian, "Only little endian is supported");

        _size = bytes.Length;

        if (bytes.Length > 8)
        {
            _heap = new ulong[(bytes.Length + 7) / 8];
            bytes.CopyTo(GetHeapBytes(bytes.Length));
            return;
        }

        Span<byte> inline = GetInlineBytes(bytes.Length);
        bytes.CopyTo(inline);
    }

    public UInt64 AsUInt64()
    {
        return _heap != null ? _heap[0] : _inline;
    }

    public UInt32 AsUInt32()
    {
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
        if (_heap != null)
        {
            return GetHeapBytes(_size);
        }

        return GetInlineBytes(_size);
    }

    [UnscopedRef]
    private Span<byte> GetHeapBytes(int size)
    {
        Debug.Assert(_heap != null);
        return MemoryMarshal.AsBytes(_heap.AsSpan()).Slice(0, size);
    }

    [UnscopedRef]
    private Span<byte> GetInlineBytes(int size)
    {
        Debug.Assert(size > 0 && size <= 8);
        return MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref _inline, 1)).Slice(0, size);
    }
}
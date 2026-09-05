using System.Text;

namespace Bson.MvtNet;

internal sealed class ProtoBuffer
{
    public const int Varint = 0;
    public const int Fixed64 = 1;
    public const int LengthDelimited = 2;
    public const int Fixed32 = 5;

    private const int MinCapacity = 256;

    private byte[] _buffer;
    private int _count;

    public ProtoBuffer(int capacity = 0)
    {
        _buffer = capacity == 0 ? Array.Empty<byte>() : new byte[capacity];
    }

    public int Count => _count;

    public void Clear() => _count = 0;

    public void WriteByte(byte value)
    {
        EnsureRoomFor(1);
        _buffer[_count++] = value;
    }

    public void WriteVarint(ulong value)
    {
        while (value >= 0x80)
        {
            WriteByte((byte)(value | 0x80));
            value >>= 7;
        }

        WriteByte((byte)value);
    }

    public void WriteFixed32(uint value)
    {
        for (int shift = 0; shift < 32; shift += 8)
        {
            WriteByte((byte)(value >> shift));
        }
    }

    public void WriteFixed64(ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            WriteByte((byte)(value >> shift));
        }
    }

    public void WriteString(string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        WriteVarint((ulong)byteCount);
        EnsureRoomFor(byteCount);
        _count += Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, _count);
    }

    public void WritePacked(ReadOnlySpan<uint> values)
    {
        WriteVarint((ulong)PackedSize(values));
        foreach (uint value in values)
        {
            WriteVarint(value);
        }
    }

    public void Write(ProtoBuffer other)
    {
        EnsureRoomFor(other._count);
        Buffer.BlockCopy(other._buffer, 0, _buffer, _count, other._count);
        _count += other._count;
    }

    public void WriteTo(Stream stream) => stream.Write(_buffer, 0, _count);

    public byte[] ToArray()
    {
        bool exactlyFull = _count == _buffer.Length;
        if (exactlyFull)
        {
            return _buffer;
        }

        var result = new byte[_count];
        Buffer.BlockCopy(_buffer, 0, result, 0, _count);
        return result;
    }

    public static int VarintSize(ulong value)
    {
        int size = 1;
        while (value >= 0x80)
        {
            size++;
            value >>= 7;
        }

        return size;
    }

    public static int PackedSize(ReadOnlySpan<uint> values)
    {
        int size = 0;
        foreach (uint value in values)
        {
            size += VarintSize(value);
        }

        return size;
    }

    private void EnsureRoomFor(int bytes)
    {
        if (_buffer.Length - _count < bytes)
        {
            Grow(bytes);
        }
    }

    private void Grow(int bytes)
    {
        int needed = _count + bytes;
        int capacity = Math.Max(Math.Max(needed, _buffer.Length * 2), MinCapacity);
        Array.Resize(ref _buffer, capacity);
    }
}

using System.Buffers.Binary;

namespace PulseOffice.Typography.MicroType;

internal sealed class FontBuffer(int capacity)
{
    private byte[] _buffer = new byte[Math.Min(capacity, 4096)];

    public int Length { get; private set; }

    public void WriteByte(byte value) => Reserve(1)[0] = value;

    public void WriteUInt16(ushort value) => BinaryPrimitives.WriteUInt16BigEndian(Reserve(2), value);

    public void WriteInt16(short value) => BinaryPrimitives.WriteInt16BigEndian(Reserve(2), value);

    public void WriteUInt32(uint value) => BinaryPrimitives.WriteUInt32BigEndian(Reserve(4), value);

    public void WriteBytes(ReadOnlySpan<byte> bytes) => bytes.CopyTo(Reserve(bytes.Length));

    public void PatchUInt16(int offset, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(_buffer.AsSpan(offset, 2), value);

    public void PatchInt16(int offset, short value) => BinaryPrimitives.WriteInt16BigEndian(_buffer.AsSpan(offset, 2), value);

    public byte[] ToArray() => _buffer.AsSpan(0, Length).ToArray();

    private Span<byte> Reserve(int count)
    {
        if (count > capacity - Length) throw new InvalidDataException("Reconstructed font exceeds the size limit.");

        var needed = Length + count;
        if (needed > _buffer.Length)
            Array.Resize(ref _buffer, (int)Math.Min(capacity, Math.Max(needed, (long)_buffer.Length * 2)));

        var span = _buffer.AsSpan(Length, count);
        Length = needed;
        return span;
    }
}

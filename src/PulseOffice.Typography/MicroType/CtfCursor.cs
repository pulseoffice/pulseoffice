namespace PulseOffice.Typography.MicroType;

internal sealed class CtfCursor
{
    private readonly byte[] _data;
    private readonly int _start;
    private readonly int _end;
    private int _position;

    public CtfCursor(byte[] data, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (start < 0 || length < 0 || start > data.Length - length)
            throw new InvalidDataException("Compact font table points outside its block.");

        _data = data;
        _start = start;
        _end = start + length;
        _position = start;
    }

    public CtfCursor(byte[] data) : this(data, 0, data.Length)
    {
    }

    public int Position => _position - _start;

    public int Remaining => _end - _position;

    public void Seek(int offset)
    {
        if (offset < 0 || offset > _end - _start) throw new InvalidDataException("Compact font offset is outside its block.");
        _position = _start + offset;
    }

    public byte PeekByte()
    {
        Require(1);
        return _data[_position];
    }

    public byte ReadByte()
    {
        Require(1);
        return _data[_position++];
    }

    public ushort ReadUInt16()
    {
        Require(2);
        var value = (ushort)((_data[_position] << 8) | _data[_position + 1]);
        _position += 2;
        return value;
    }

    public short ReadInt16() => unchecked((short)ReadUInt16());

    public uint ReadUInt32() => ((uint)ReadUInt16() << 16) | ReadUInt16();

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        if (count < 0) throw new InvalidDataException("Negative byte count in compact font data.");
        Require(count);
        var span = _data.AsSpan(_position, count);
        _position += count;
        return span;
    }

    public LeastSignificantFirstBitReader RemainingBits() => new(_data, _position, _end - _position);

    public void SkipPast(BitReader bits)
    {
        ArgumentNullException.ThrowIfNull(bits);
        if (bits.NextByteOffset < _position || bits.NextByteOffset > _end) throw new InvalidDataException("Compact font offset is outside its block.");
        _position = bits.NextByteOffset;
    }

    public ushort Read255UShort()
    {
        var code = ReadByte();
        return code switch
        {
            253 => ReadUInt16(),
            255 => (ushort)(253 + ReadByte()),
            254 => (ushort)(506 + ReadByte()),
            _ => code
        };
    }

    public short Read255Short()
    {
        var code = ReadByte();
        if (code == 253) return ReadInt16();

        var sign = 1;
        if (code == 250)
        {
            sign = -1;
            code = ReadByte();
        }

        var magnitude = code switch
        {
            255 => 250 + ReadByte(),
            254 => 500 + ReadByte(),
            _ => code
        };
        return (short)(sign * magnitude);
    }

    private void Require(int count)
    {
        if (count > _end - _position) throw new InvalidDataException("Compact font data is truncated.");
    }
}

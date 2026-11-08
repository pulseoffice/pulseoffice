namespace PulseOffice.Typography.MicroType;

internal abstract class BitReader
{
    private readonly byte[] _data;
    private readonly int _end;
    private int _next;
    private int _bit = 8;
    private byte _current;

    protected BitReader(byte[] data, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (start < 0 || length < 0 || start > data.Length - length)
            throw new InvalidDataException("Compressed font block points outside the data.");

        _data = data;
        _next = start;
        _end = start + length;
    }

    public int NextByteOffset => _next;

    public bool ReadBit()
    {
        if (_bit == 8)
        {
            if (_next >= _end) throw new InvalidDataException("Compressed font data ends unexpectedly.");
            _current = _data[_next++];
            _bit = 0;
        }

        return BitAt(_current, _bit++);
    }

    public uint ReadBits(int count)
    {
        if (count is < 0 or > 32) throw new InvalidDataException("Invalid bit field width in compressed font data.");

        var value = 0u;
        for (var index = 0; index < count; index++)
            value = (value << 1) | (ReadBit() ? 1u : 0u);
        return value;
    }

    public void AlignToByte() => _bit = 8;

    protected abstract bool BitAt(byte value, int index);
}

internal sealed class MostSignificantFirstBitReader(byte[] data, int start, int length) : BitReader(data, start, length)
{
    protected override bool BitAt(byte value, int index) => ((value >> (7 - index)) & 1) != 0;
}

internal sealed class LeastSignificantFirstBitReader(byte[] data, int start, int length) : BitReader(data, start, length)
{
    private const int MaxMagnitude = 1 << 16;

    protected override bool BitAt(byte value, int index) => ((value >> index) & 1) != 0;

    public int ReadMagnitude()
    {
        var magnitude = 0;
        while (ReadBit())
            if (++magnitude > MaxMagnitude) throw new InvalidDataException("Magnitude encoded value is too large.");

        if (magnitude == 0) return 0;
        return ReadBit() ? -magnitude : magnitude;
    }
}

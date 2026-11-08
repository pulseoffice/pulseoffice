namespace PulseOffice.Typography.OpenType;

internal sealed class BigEndianSource(Stream stream)
{
    private readonly byte[] _buffer = new byte[8];

    public long Length => stream.Length;

    public byte[] Bytes(long offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > stream.Length)
            throw new InvalidDataException("Font table points outside the file.");

        var bytes = new byte[count];
        stream.Position = offset;
        stream.ReadExactly(bytes);
        return bytes;
    }

    public ushort UInt16(long offset)
    {
        Fill(offset, 2);
        return (ushort)((_buffer[0] << 8) | _buffer[1]);
    }

    public short Int16(long offset) => unchecked((short)UInt16(offset));

    public uint UInt32(long offset)
    {
        Fill(offset, 4);
        return ((uint)_buffer[0] << 24) | ((uint)_buffer[1] << 16) | ((uint)_buffer[2] << 8) | _buffer[3];
    }

    public string Tag(long offset)
    {
        Fill(offset, 4);
        return string.Create(4, _buffer, (span, source) =>
        {
            for (var index = 0; index < 4; index++) span[index] = (char)source[index];
        });
    }

    private void Fill(long offset, int count)
    {
        if (offset < 0 || offset + count > stream.Length)
            throw new InvalidDataException("Font data is truncated.");

        stream.Position = offset;
        stream.ReadExactly(_buffer, 0, count);
    }
}

using System.Buffers.Binary;

namespace PulseOffice.Typography.OpenType;

public interface IFontFileExtractor
{
    byte[] Extract(byte[] data, int collectionIndex);
}

public sealed class FontFileExtractor(OpenTypeReader reader) : IFontFileExtractor
{
    public FontFileExtractor() : this(new OpenTypeReader())
    {
    }

    public byte[] Extract(byte[] data, int collectionIndex)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 12 || BinaryPrimitives.ReadUInt32BigEndian(data) != 0x74746366) return data;

        using var stream = new MemoryStream(data, writable: false);
        var tables = reader.Tables(stream, collectionIndex);
        var faceOffset = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12 + (collectionIndex * 4)));
        if ((long)faceOffset + 4 > data.Length) throw new InvalidDataException("Font collection face is truncated.");
        var version = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan((int)faceOffset));

        if (tables.Any(table => (long)table.Offset + table.Length > data.Length))
            throw new InvalidDataException("Font table is truncated.");
        var headerLength = 12 + (tables.Count * 16);
        var total = headerLength + tables.Sum(table => (long)Padded(table.Length));
        if (total > int.MaxValue || total > (2L * data.Length) + headerLength) throw new InvalidDataException("Font collection face is too large.");
        var output = new byte[(int)total];

        BinaryPrimitives.WriteUInt32BigEndian(output, version);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4), (ushort)tables.Count);
        var power = 1;
        var exponent = 0;
        while (power * 2 <= tables.Count)
        {
            power *= 2;
            exponent++;
        }
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(6), (ushort)(power * 16));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(8), (ushort)exponent);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(10), (ushort)((tables.Count * 16) - (power * 16)));

        var cursor = headerLength;
        var ordered = tables.OrderBy(table => table.Tag, StringComparer.Ordinal).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var table = ordered[index];

            var record = output.AsSpan(12 + (index * 16));
            for (var letter = 0; letter < 4; letter++) record[letter] = (byte)table.Tag[letter];
            BinaryPrimitives.WriteUInt32BigEndian(record[4..], table.CheckSum);
            BinaryPrimitives.WriteUInt32BigEndian(record[8..], (uint)cursor);
            BinaryPrimitives.WriteUInt32BigEndian(record[12..], table.Length);

            data.AsSpan((int)table.Offset, (int)table.Length).CopyTo(output.AsSpan(cursor));
            cursor += (int)Padded(table.Length);
        }

        return output;
    }

    private long Padded(uint length) => ((long)length + 3) & ~3L;
}

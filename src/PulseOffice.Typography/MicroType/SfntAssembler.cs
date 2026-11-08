using System.Buffers.Binary;

namespace PulseOffice.Typography.MicroType;

internal interface ISfntAssembler
{
    byte[] Assemble(uint scalarType, IReadOnlyList<SfntTable> tables);
}

internal sealed class SfntAssembler(MtxLimits limits) : ISfntAssembler
{
    private const uint ChecksumMagic = 0xB1B0AFBA;
    private const int HeadAdjustmentOffset = 8;

    public byte[] Assemble(uint scalarType, IReadOnlyList<SfntTable> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        var ordered = tables.OrderBy(table => table.Tag, StringComparer.Ordinal).ToList();
        var count = ordered.Count;
        var directoryEnd = 12 + (16 * count);

        long total = directoryEnd;
        foreach (var table in ordered) total += Padded(table.Data.Length);
        if (total > limits.MaxFontBytes) throw new InvalidDataException("Reconstructed font exceeds the size limit.");

        var font = new byte[total];
        var span = font.AsSpan();
        var power = 1;
        var exponent = 0;
        while (power * 2 <= count)
        {
            power *= 2;
            exponent++;
        }

        BinaryPrimitives.WriteUInt32BigEndian(span, scalarType);
        BinaryPrimitives.WriteUInt16BigEndian(span[4..], (ushort)count);
        BinaryPrimitives.WriteUInt16BigEndian(span[6..], (ushort)(power * 16));
        BinaryPrimitives.WriteUInt16BigEndian(span[8..], (ushort)exponent);
        BinaryPrimitives.WriteUInt16BigEndian(span[10..], (ushort)((count * 16) - (power * 16)));

        var offset = directoryEnd;
        var headOffset = -1;
        for (var index = 0; index < count; index++)
        {
            var table = ordered[index];
            table.Data.CopyTo(span[offset..]);
            if (table.Tag == "head" && table.Data.Length >= HeadAdjustmentOffset + 4)
            {
                headOffset = offset;
                span.Slice(offset + HeadAdjustmentOffset, 4).Clear();
            }

            var record = span[(12 + (16 * index))..];
            for (var letter = 0; letter < 4; letter++) record[letter] = (byte)table.Tag[letter];
            BinaryPrimitives.WriteUInt32BigEndian(record[4..], Checksum(span.Slice(offset, Padded(table.Data.Length))));
            BinaryPrimitives.WriteUInt32BigEndian(record[8..], (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(record[12..], (uint)table.Data.Length);
            offset += Padded(table.Data.Length);
        }

        if (headOffset >= 0)
            BinaryPrimitives.WriteUInt32BigEndian(span[(headOffset + HeadAdjustmentOffset)..], unchecked(ChecksumMagic - Checksum(span)));

        return font;
    }

    private int Padded(int length) => (length + 3) & ~3;

    private uint Checksum(ReadOnlySpan<byte> data)
    {
        var sum = 0u;
        for (var index = 0; index + 4 <= data.Length; index += 4)
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(data[index..]));
        return sum;
    }
}

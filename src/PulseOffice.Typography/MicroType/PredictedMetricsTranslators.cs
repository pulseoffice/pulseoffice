namespace PulseOffice.Typography.MicroType;

internal abstract class PredictedMetricsTranslator : CtfTableTranslator
{
    private const ushort UncompressedThreshold = 0x8000;

    public override IEnumerable<SfntTable> Translate(CtfFont font, CtfTableEntry entry)
    {
        byte[] data;
        try
        {
            var version = font.Table(entry).ReadUInt16();
            data = version >= UncompressedThreshold ? Restore(font.Bytes(entry), version) : Expand(font, font.Table(entry));
        }
        catch (InvalidDataException)
        {
            return [];
        }

        return [new SfntTable(entry.Tag, data)];
    }

    protected abstract byte[] Expand(CtfFont font, CtfCursor input);

    private byte[] Restore(byte[] data, ushort version)
    {
        var real = (ushort)(0xFFFF - version);
        data[0] = (byte)(real >> 8);
        data[1] = (byte)real;
        return data;
    }
}

internal sealed class HdmxTableTranslator : PredictedMetricsTranslator
{
    public override bool Handles(string tag) => tag == "hdmx";

    protected override byte[] Expand(CtfFont font, CtfCursor input)
    {
        var version = input.ReadUInt16();
        var records = input.ReadInt16();
        var recordSize = (int)input.ReadUInt32();
        var glyphs = font.GlyphCount;
        if (records < 0 || recordSize < glyphs + 2 || recordSize > glyphs + 8)
            throw new InvalidDataException("Compressed 'hdmx' table has an inconsistent header.");

        var sizes = new (byte Ppem, byte MaxWidth)[records];
        for (var index = 0; index < records; index++) sizes[index] = (input.ReadByte(), input.ReadByte());

        var unitsPerEm = font.UnitsPerEm;
        var stream = input.ReadBytes(input.Remaining).ToArray();
        var bits = new LeastSignificantFirstBitReader(stream, 0, stream.Length);
        var output = new FontBuffer(font.Limits.MaxFontBytes);
        output.WriteUInt16(version);
        output.WriteInt16(records);
        output.WriteUInt32((uint)recordSize);

        foreach (var (ppem, maxWidth) in sizes)
        {
            output.WriteByte(ppem);
            output.WriteByte(maxWidth);
            for (var glyph = 0; glyph < glyphs; glyph++)
            {
                var predicted = ((((64L * ppem * font.AdvanceWidth(glyph)) + (unitsPerEm / 2)) / unitsPerEm) + 32) / 64;
                output.WriteByte(unchecked((byte)(predicted + bits.ReadMagnitude())));
            }

            for (var pad = glyphs + 2; pad < recordSize; pad++) output.WriteByte(0);
        }

        return output.ToArray();
    }
}

internal sealed class VdmxTableTranslator : PredictedMetricsTranslator
{
    private const int FirstPredictedPpem = 8;

    public override bool Handles(string tag) => tag == "VDMX";

    protected override byte[] Expand(CtfFont font, CtfCursor input)
    {
        var version = input.ReadUInt16();
        var groups = input.ReadUInt16();
        var ratios = input.ReadUInt16();
        var ratioRecords = input.ReadBytes(4 * ratios).ToArray();
        var ratioOffsets = new int[ratios];
        for (var index = 0; index < ratios; index++) ratioOffsets[index] = input.ReadUInt16();
        if (ratios == 0 || groups == 0) throw new InvalidDataException("Compressed 'VDMX' table has no groups.");

        input.Seek(ratioOffsets[0]);
        var compactStarts = new int[groups];
        var decoded = new List<(int Ppem, short YMax, short YMin)[]>(groups);
        for (var group = 0; group < groups; group++)
        {
            compactStarts[group] = input.Position;
            decoded.Add(ReadGroup(input));
        }

        var headerLength = 6 + (6 * ratios);
        var starts = new int[groups];
        var position = headerLength;
        for (var group = 0; group < groups; group++)
        {
            starts[group] = position;
            position += 4 + (6 * decoded[group].Length);
        }

        if (position > ushort.MaxValue) throw new InvalidDataException("Reconstructed 'VDMX' table is too large.");

        var output = new FontBuffer(position);
        output.WriteUInt16(version);
        output.WriteUInt16(groups);
        output.WriteUInt16(ratios);
        output.WriteBytes(ratioRecords);
        foreach (var offset in ratioOffsets)
        {
            var group = Array.IndexOf(compactStarts, offset);
            if (group < 0) throw new InvalidDataException("Compressed 'VDMX' ratio points between groups.");
            output.WriteUInt16((ushort)starts[group]);
        }

        foreach (var entries in decoded)
        {
            output.WriteUInt16((ushort)entries.Length);
            output.WriteByte(entries.Length == 0 ? (byte)0 : (byte)Math.Min(entries[0].Ppem, 255));
            output.WriteByte(entries.Length == 0 ? (byte)0 : (byte)Math.Min(entries[^1].Ppem, 255));
            foreach (var (ppem, yMax, yMin) in entries)
            {
                output.WriteUInt16((ushort)ppem);
                output.WriteInt16(yMax);
                output.WriteInt16(yMin);
            }
        }

        return output.ToArray();
    }

    private (int Ppem, short YMax, short YMin)[] ReadGroup(CtfCursor input)
    {
        var records = input.ReadUInt16();
        var yMaxMultiplier = input.ReadInt16();
        var yMinMultiplier = input.ReadInt16();
        var bits = input.RemainingBits();

        var entries = new (int Ppem, short YMax, short YMin)[records];
        var predicted = FirstPredictedPpem;
        for (var index = 0; index < records; index++)
        {
            var ppem = predicted + bits.ReadMagnitude();
            if (ppem is < 0 or > ushort.MaxValue) throw new InvalidDataException("Compressed 'VDMX' pel height is out of range.");

            var yMax = (((long)ppem * yMaxMultiplier) + 1024) / 2048;
            var yMin = -((((long)ppem * yMinMultiplier) + 1024) / 2048);
            entries[index] = (ppem, unchecked((short)(yMax + bits.ReadMagnitude())), unchecked((short)(yMin + bits.ReadMagnitude())));
            predicted = ppem + 1;
        }

        input.SkipPast(bits);
        return entries;
    }
}

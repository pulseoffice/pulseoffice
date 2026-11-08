namespace PulseOffice.Typography.MicroType;

internal abstract class CtfTableTranslator
{
    public abstract bool Handles(string tag);

    public abstract IEnumerable<SfntTable> Translate(CtfFont font, CtfTableEntry entry);
}

internal sealed class CopiedTableTranslator : CtfTableTranslator
{
    public override bool Handles(string tag) => true;

    public override IEnumerable<SfntTable> Translate(CtfFont font, CtfTableEntry entry) => [new SfntTable(entry.Tag, font.Bytes(entry))];
}

internal sealed class LocaTableTranslator : CtfTableTranslator
{
    public override bool Handles(string tag) => tag == "loca";

    public override IEnumerable<SfntTable> Translate(CtfFont font, CtfTableEntry entry) => [];
}

internal sealed class CvtTableTranslator : CtfTableTranslator
{
    private const byte WordCode = 238;
    private const byte NegativeZero = 239;
    private const byte PositiveOne = 248;
    private const int Step = 238;

    public override bool Handles(string tag) => tag == "cvt ";

    public override IEnumerable<SfntTable> Translate(CtfFont font, CtfTableEntry entry)
    {
        var input = font.Table(entry);
        var count = input.ReadUInt16();
        if (count > input.Remaining) throw new InvalidDataException("Compressed 'cvt ' table declares more values than it holds.");

        var output = new FontBuffer(count * 2);
        short last = 0;
        for (var index = 0; index < count; index++)
        {
            last = unchecked((short)(last + Delta(input)));
            output.WriteInt16(last);
        }

        return [new SfntTable(entry.Tag, output.ToArray())];
    }

    private int Delta(CtfCursor input)
    {
        var code = input.ReadByte();
        if (code >= PositiveOne) return (Step * (code - (PositiveOne - 1))) + input.ReadByte();
        if (code >= NegativeZero) return -((Step * (code - NegativeZero)) + input.ReadByte());
        if (code == WordCode) return input.ReadInt16();
        return code;
    }
}

internal sealed class GlyfTableTranslator(IReadOnlyList<GlyphDecoder> decoders) : CtfTableTranslator
{
    private const int ShortLocaLimit = 0x1FFFE;

    public override bool Handles(string tag) => tag == "glyf";

    public override IEnumerable<SfntTable> Translate(CtfFont font, CtfTableEntry entry)
    {
        var count = font.GlyphCount;
        var glyph = font.TableToEnd(entry);
        var glyf = new FontBuffer(font.Limits.MaxFontBytes);
        var offsets = new int[count + 1];

        for (var index = 0; index < count; index++)
        {
            var contours = glyph.ReadInt16();
            var decoder = decoders.FirstOrDefault(candidate => candidate.Handles(contours))
                ?? throw new InvalidDataException("No decoder for compact glyph.");
            decoder.Decode(contours, glyph, font, glyf);
            if (glyf.Length % 2 != 0) glyf.WriteByte(0);
            offsets[index + 1] = glyf.Length;
        }

        font.LongLoca = !font.ShortLoca || glyf.Length > ShortLocaLimit;
        var loca = new FontBuffer((count + 1) * 4);
        foreach (var offset in offsets)
        {
            if (font.LongLoca) loca.WriteUInt32((uint)offset);
            else loca.WriteUInt16((ushort)(offset / 2));
        }

        return [new SfntTable("glyf", glyf.ToArray()), new SfntTable("loca", loca.ToArray())];
    }
}

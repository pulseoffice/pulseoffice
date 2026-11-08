namespace PulseOffice.Typography.MicroType;

internal abstract class GlyphDecoder
{
    public abstract bool Handles(short contours);

    public abstract void Decode(short contours, CtfCursor glyph, CtfFont font, FontBuffer output);
}

internal sealed class SimpleGlyphDecoder(TripletTable triplets, GlyphProgramDecoder program) : GlyphDecoder
{
    private const short ExplicitBoundsMarker = 0x7FFF;
    private const byte OnCurve = 0x01;
    private const byte XShort = 0x02;
    private const byte YShort = 0x04;
    private const byte XSame = 0x10;
    private const byte YSame = 0x20;

    public override bool Handles(short contours) => contours >= 0;

    public override void Decode(short contours, CtfCursor glyph, CtfFont font, FontBuffer output)
    {
        var explicitBounds = contours == ExplicitBoundsMarker;
        short xMin = 0, yMin = 0, xMax = 0, yMax = 0;
        if (explicitBounds)
        {
            contours = glyph.ReadInt16();
            xMin = glyph.ReadInt16();
            yMin = glyph.ReadInt16();
            xMax = glyph.ReadInt16();
            yMax = glyph.ReadInt16();
            if (contours < 0) throw new InvalidDataException("Simple glyph declares a negative contour count.");
        }

        if (contours == 0) return;

        output.WriteInt16(contours);
        var boundsAt = output.Length;
        output.WriteInt16(xMin);
        output.WriteInt16(yMin);
        output.WriteInt16(xMax);
        output.WriteInt16(yMax);

        var points = 0;
        for (var contour = 0; contour < contours; contour++)
        {
            points += glyph.Read255UShort() + (contour == 0 ? 1 : 0);
            if (points > font.Limits.MaxPointsPerGlyph) throw new InvalidDataException("Glyph has too many points.");
            output.WriteUInt16((ushort)(points - 1));
        }

        var flags = glyph.ReadBytes(points).ToArray();
        var xs = new short[points];
        var ys = new short[points];
        short x = 0, y = 0;
        short minX = short.MaxValue, minY = short.MaxValue, maxX = short.MinValue, maxY = short.MinValue;
        for (var point = 0; point < points; point++)
        {
            var (dx, dy) = triplets[flags[point]].Read(glyph);
            xs[point] = unchecked((short)dx);
            ys[point] = unchecked((short)dy);
            x = unchecked((short)(x + xs[point]));
            y = unchecked((short)(y + ys[point]));
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        program.Write(glyph, font, output);

        for (var point = 0; point < points; point++)
            output.WriteByte(Flags(xs[point], ys[point], (flags[point] & 0x80) == 0, point == 0));

        WriteCoordinates(xs, output);
        WriteCoordinates(ys, output);

        if (explicitBounds) return;
        output.PatchInt16(boundsAt, minX);
        output.PatchInt16(boundsAt + 2, minY);
        output.PatchInt16(boundsAt + 4, maxX);
        output.PatchInt16(boundsAt + 6, maxY);
    }

    private byte Flags(short x, short y, bool onCurve, bool first)
    {
        var flags = onCurve ? OnCurve : (byte)0;
        flags |= Axis(x, first, XShort, XSame);
        flags |= Axis(y, first, YShort, YSame);
        return flags;
    }

    private byte Axis(short value, bool first, byte shortFlag, byte sameFlag)
    {
        if (!first && value == 0) return sameFlag;
        if (value is > -256 and < 0) return shortFlag;
        if (value is >= 0 and < 256) return (byte)(shortFlag | sameFlag);
        return 0;
    }

    private void WriteCoordinates(short[] deltas, FontBuffer output)
    {
        for (var index = 0; index < deltas.Length; index++)
        {
            var value = deltas[index];
            if (index != 0 && value == 0) continue;
            if (value is > -256 and < 256) output.WriteByte((byte)Math.Abs(value));
            else output.WriteInt16(value);
        }
    }
}

internal sealed class CompositeGlyphDecoder(GlyphProgramDecoder program) : GlyphDecoder
{
    private const ushort ArgumentsAreWords = 0x0001;
    private const ushort HasScale = 0x0008;
    private const ushort MoreComponents = 0x0020;
    private const ushort HasXyScale = 0x0040;
    private const ushort HasTwoByTwo = 0x0080;
    private const ushort HasInstructions = 0x0100;

    public override bool Handles(short contours) => contours < 0;

    public override void Decode(short contours, CtfCursor glyph, CtfFont font, FontBuffer output)
    {
        output.WriteInt16(-1);
        output.WriteBytes(glyph.ReadBytes(8));

        ushort flags;
        var components = 0;
        do
        {
            if (++components > font.Limits.MaxComponentsPerGlyph) throw new InvalidDataException("Composite glyph has too many components.");

            flags = glyph.ReadUInt16();
            output.WriteUInt16(flags);
            output.WriteBytes(glyph.ReadBytes(2));
            output.WriteBytes(glyph.ReadBytes((flags & ArgumentsAreWords) != 0 ? 4 : 2));
            output.WriteBytes(glyph.ReadBytes(TransformLength(flags)));
        }
        while ((flags & MoreComponents) != 0);

        if ((flags & HasInstructions) != 0) program.Write(glyph, font, output);
    }

    private int TransformLength(ushort flags) =>
        (flags & HasTwoByTwo) != 0 ? 8 : (flags & HasXyScale) != 0 ? 4 : (flags & HasScale) != 0 ? 2 : 0;
}

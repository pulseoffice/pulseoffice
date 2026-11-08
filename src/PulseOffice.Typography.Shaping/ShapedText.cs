namespace PulseOffice.Typography.Shaping;

public readonly record struct ShapedGlyph(int Glyph, int Cluster, double Advance, double OffsetX, double OffsetY);

public readonly record struct ShapingSpan(int Start, int Length)
{
    public int End => Start + Length;
}

public sealed record ShapedText(IReadOnlyList<ShapedGlyph> Glyphs, bool RightToLeft)
{
    public static ShapedText Empty { get; } = new([], false);

    public double Width => Glyphs.Sum(glyph => glyph.Advance);

    public bool Complete => Glyphs.Count > 0 && Glyphs.All(glyph => glyph.Glyph > 0);
}

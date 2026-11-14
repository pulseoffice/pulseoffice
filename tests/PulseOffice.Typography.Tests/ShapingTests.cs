using PulseOffice.Platform;
using PulseOffice.Typography.OpenType;
using PulseOffice.Typography.OpenType.Metrics;
using PulseOffice.Typography.Shaping;
using Xunit;

namespace PulseOffice.Typography.Tests;

public sealed class ShapingTests
{
    private static readonly Lazy<IndexedFontLocator> Installed = new(() =>
        new IndexedFontLocator(new SystemFontDirectories(new PulseOffice.Platform.SystemEnvironmentVariables(), ((PulseOffice.Platform.IHostSystem)new PulseOffice.Platform.RuntimeHostSystem()).Family), new OpenTypeReader(), new FontMetricsReader()));

    private static FontMetricsFace? Face(int codePoint, params string[] families) =>
        families.Select(family => Installed.Value.Find(family, false, false))
            .FirstOrDefault(face => face is not null && face.GlyphOf(codePoint) > 0);

    [Fact]
    public void ArabicPhraseIsOneSpanAndDigitsSplitIt()
    {
        var segmenter = new ComplexScriptSegmenter();

        Assert.Equal([new ShapingSpan(4, 9)], segmenter.Spans("abc سلام عليك."));
        Assert.Equal(2, segmenter.Spans("سلام 123 عليك").Count);
        Assert.Empty(segmenter.Spans("Plain Latin text, 42."));
    }

    [Fact]
    public void EmojiJoinerSequencesAndFlagsAreSpans()
    {
        var segmenter = new ComplexScriptSegmenter();
        var family = "a \U0001F468‍\U0001F469‍\U0001F467 b";

        Assert.Equal([new ShapingSpan(2, 8)], segmenter.Spans(family));
        Assert.Single(segmenter.Spans("\U0001F1EE\U0001F1F9"));
        Assert.Empty(segmenter.Spans("\U0001F600"));
    }

    [Fact]
    public void LoneSurrogatesDoNotThrow()
    {
        Assert.Empty(new ComplexScriptSegmenter().Spans("a\uD800b\uDC00"));
    }

    [Fact]
    public void ArabicLettersTakeTheirJoiningForms()
    {
        const string text = "سلام";
        if (Face('س', "Geeza Pro", "Noto Sans Arabic", "Arial Unicode MS", "SF Arabic", "DejaVu Sans") is not { } face) return;
        using var shaper = new HarfBuzzTextShaper();

        var shaped = shaper.Shape(face, text, new ShapingSpan(0, text.Length));
        var isolated = text.Select(character => face.GlyphOf(character)).ToHashSet();

        Assert.True(shaped.Complete);
        Assert.True(shaped.RightToLeft);
        Assert.Contains(shaped.Glyphs, glyph => !isolated.Contains(glyph.Glyph));
        Assert.Equal(text.Length - 1, shaped.Glyphs[0].Cluster);
    }

    [Fact]
    public void DevanagariConjunctsAndVowelSignsAreReordered()
    {
        const string text = "क्षि";
        if (Face('क', "Kohinoor Devanagari", "Devanagari Sangam MN", "Noto Sans Devanagari", "Mangal", "Lohit Devanagari") is not { } face) return;
        using var shaper = new HarfBuzzTextShaper();

        var shaped = shaper.Shape(face, text, new ShapingSpan(0, text.Length));

        Assert.True(shaped.Complete);
        Assert.True(shaped.Glyphs.Count < text.Length);
    }

    [Fact]
    public void InvalidSpansGiveNothing()
    {
        if (Face('a', "Helvetica", "Arial", "DejaVu Sans", "Liberation Sans") is not { } face) return;
        using var shaper = new HarfBuzzTextShaper(new TextShaperOptions { CachedFaces = 1 });

        Assert.Empty(shaper.Shape(face, "abc", new ShapingSpan(2, 5)).Glyphs);
        Assert.Empty(shaper.Shape(face, "abc", new ShapingSpan(-1, 1)).Glyphs);
        Assert.Equal(3, shaper.Shape(face, "abc", new ShapingSpan(0, 3)).Glyphs.Count);
    }
}

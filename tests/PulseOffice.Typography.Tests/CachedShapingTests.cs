using PulseOffice.Platform;
using PulseOffice.Typography.OpenType;
using PulseOffice.Typography.OpenType.Metrics;
using PulseOffice.Typography.Shaping;
using Xunit;

namespace PulseOffice.Typography.Tests;

public sealed class CachedShapingTests
{
    private static readonly Lazy<IndexedFontLocator> Installed = new(() =>
        new IndexedFontLocator(new SystemFontDirectories(), new OpenTypeReader(), new FontMetricsReader()));

    private sealed class CountingShaper(ITextShaper inner) : ITextShaper
    {
        public int Calls { get; private set; }

        public ShapedText Shape(FontMetricsFace face, string text, ShapingSpan span)
        {
            Calls++;
            return inner.Shape(face, text, span);
        }
    }

    private static FontMetricsFace? Arabic() =>
        new[] { "Geeza Pro", "Noto Sans Arabic", "Arial Unicode MS", "DejaVu Sans" }.Select(family => Installed.Value.Find(family, false, false))
            .FirstOrDefault(face => face is not null && face.GlyphOf('س') > 0);

    [Fact]
    public void RepeatedRunsAreShapedOnce()
    {
        if (Arabic() is not { } face) return;
        using var harfBuzz = new HarfBuzzTextShaper();
        var counting = new CountingShaper(harfBuzz);
        var cached = new CachedTextShaper(counting);
        const string text = "abc سلام عليك.";
        var span = new ShapingSpan(4, 9);

        var first = cached.Shape(face, text, span);
        for (var i = 0; i < 100; i++) Assert.Same(first, cached.Shape(face, new string(text.AsSpan()), span));
        cached.Shape(face, text, new ShapingSpan(4, 4));

        Assert.Equal(2, counting.Calls);
        Assert.True(first.Complete);
    }

    [Fact]
    public void TheCacheIsBounded()
    {
        if (Arabic() is not { } face) return;
        using var harfBuzz = new HarfBuzzTextShaper();
        var cached = new CachedTextShaper(harfBuzz, new ShapedRunCacheOptions { CachedRuns = 16 });

        for (var i = 0; i < 100; i++) cached.Shape(face, $"سلام {i}", new ShapingSpan(0, 4));

        Assert.InRange(cached.Count, 1, 16);
    }
}

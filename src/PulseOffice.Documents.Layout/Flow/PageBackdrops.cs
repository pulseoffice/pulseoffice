using PulseOffice.Documents.Layout.Measurement;
using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.Layout.Flow;

public interface IPageBackdrops
{
    IReadOnlyList<LayoutBox> Build(PageGeometry geometry);
}

public sealed class PageBackdrops(IBorderStrokes strokes, ITextMeasurer measurer) : IPageBackdrops
{
    private const double Probe = 100;
    private const double Reach = 0.8;

    private readonly Dictionary<PageGeometry, IReadOnlyList<LayoutBox>> _cache = [];

    public IReadOnlyList<LayoutBox> Build(PageGeometry geometry)
    {
        var decor = geometry.Decor;
        if (decor.IsEmpty) return [];
        if (_cache.TryGetValue(geometry, out var cached)) return cached;
        var boxes = new List<LayoutBox>();
        if (decor.Background is { } background && background >> 24 != 0)
            boxes.Add(new DecorationBox(DecorationKind.PageBackground, new LayoutRect(0, 0, geometry.Width, geometry.Height), background, 0));
        if (decor.Watermark is { Text.Length: > 0 } watermark) boxes.Add(Watermark(geometry, watermark));
        if (decor.Borders is { HasLines: true } borders)
            foreach (var line in strokes.Lines(decor.Frame, borders))
                boxes.Add(new DecorationBox(DecorationKind.PageBorder, line.Bounds, line.Line.Color, line.Line.Width) { LineStyle = line.Line.Style });
        _cache[geometry] = boxes;
        return boxes;
    }

    private WatermarkBox Watermark(PageGeometry geometry, Styles.PageWatermark watermark)
    {
        var probe = new FontKey(watermark.Font, Probe, false, false);
        var width = Math.Max(1, measurer.Advances(probe, watermark.Text).Sum());
        var angle = double.IsFinite(watermark.Angle) ? watermark.Angle : 0;
        var radians = angle * Math.PI / 180;
        var across = Math.Abs(Math.Cos(radians)) < 0.01 ? double.MaxValue : geometry.Content.Width / Math.Abs(Math.Cos(radians));
        var down = Math.Abs(Math.Sin(radians)) < 0.01 ? double.MaxValue : geometry.Content.Height / Math.Abs(Math.Sin(radians));
        var span = Math.Min(across, down);
        var size = Math.Clamp(Probe * span * Reach / width, 8, 400);
        var font = probe with { Size = size };
        var advances = measurer.Advances(font, watermark.Text);
        var metrics = measurer.Metrics(font);
        var run = new GlyphRun(GlyphRunKind.Text, font, watermark.Text, 0, 0, 0, advances);
        var centerX = geometry.Content.X + geometry.Content.Width / 2;
        var centerY = geometry.Content.Y + geometry.Content.Height / 2;
        var half = advances.Sum() / 2;
        var lift = (metrics.Ascent - metrics.Descent) / 2;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var x = centerX - half * cos + lift * sin;
        var baseline = centerY + half * sin + lift * cos;
        return new WatermarkBox(run, x, baseline, angle, Faded(watermark.Color, watermark.Transparency));
    }

    private uint Faded(uint color, double transparency)
    {
        var keep = 1 - (double.IsFinite(transparency) ? Math.Clamp(transparency, 0, 100) : 0) / 100;
        uint Channel(int shift) => (uint)Math.Round(255 - (255 - ((color >> shift) & 0xFF)) * keep);
        return 0xFF000000 | Channel(16) << 16 | Channel(8) << 8 | Channel(0);
    }
}

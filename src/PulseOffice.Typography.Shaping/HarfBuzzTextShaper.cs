using HarfBuzzSharp;
using PulseOffice.Typography.OpenType.Metrics;
using HarfBuzzBuffer = HarfBuzzSharp.Buffer;
using HarfBuzzFont = HarfBuzzSharp.Font;

namespace PulseOffice.Typography.Shaping;

public interface ITextShaper
{
    ShapedText Shape(FontMetricsFace face, string text, ShapingSpan span);
}

public sealed record TextShaperOptions
{
    public int CachedFaces { get; init; } = 12;
}

public sealed class HarfBuzzTextShaper(TextShaperOptions options) : ITextShaper, IDisposable
{
    private readonly object _gate = new();
    private readonly LinkedList<ShapingFace> _faces = [];
    private bool _disposed;

    public HarfBuzzTextShaper() : this(new TextShaperOptions())
    {
    }

    public ShapedText Shape(FontMetricsFace face, string text, ShapingSpan span)
    {
        ArgumentNullException.ThrowIfNull(face);
        ArgumentNullException.ThrowIfNull(text);
        if (span.Start < 0 || span.Length <= 0 || span.End > text.Length) return ShapedText.Empty;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var shaping = Face(face);
            using var buffer = new HarfBuzzBuffer();
            buffer.AddUtf16(text, span.Start, span.Length);
            buffer.GuessSegmentProperties();
            shaping.Font.Shape(buffer);

            var infos = buffer.GlyphInfos;
            var positions = buffer.GlyphPositions;
            var em = (double)face.UnitsPerEm;
            var glyphs = new ShapedGlyph[infos.Length];
            for (var i = 0; i < infos.Length; i++)
            {
                glyphs[i] = new ShapedGlyph(
                    (int)infos[i].Codepoint,
                    (int)infos[i].Cluster,
                    positions[i].XAdvance / em,
                    positions[i].XOffset / em,
                    positions[i].YOffset / em);
            }

            return new ShapedText(glyphs, buffer.Direction == Direction.RightToLeft);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var face in _faces) face.Dispose();
            _faces.Clear();
        }
    }

    private ShapingFace Face(FontMetricsFace metrics)
    {
        for (var node = _faces.First; node is not null; node = node.Next)
        {
            if (!ReferenceEquals(node.Value.Metrics, metrics)) continue;
            _faces.Remove(node);
            _faces.AddFirst(node);
            return node.Value;
        }

        var created = new ShapingFace(metrics);
        _faces.AddFirst(created);
        while (_faces.Count > Math.Max(1, options.CachedFaces))
        {
            _faces.Last!.Value.Dispose();
            _faces.RemoveLast();
        }

        return created;
    }

    private sealed class ShapingFace : IDisposable
    {
        private readonly Blob _blob;
        private readonly Face _face;

        public ShapingFace(FontMetricsFace metrics)
        {
            Metrics = metrics;
            using var stream = new MemoryStream(metrics.Tables.Whole(), writable: false);
            _blob = Blob.FromStream(stream);
            _face = new Face(_blob, 0);
            Font = new HarfBuzzFont(_face);
            Font.SetScale(metrics.UnitsPerEm, metrics.UnitsPerEm);
        }

        public FontMetricsFace Metrics { get; }

        public HarfBuzzFont Font { get; }

        public void Dispose()
        {
            Font.Dispose();
            _face.Dispose();
            _blob.Dispose();
        }
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.UI.Fonts;
using PulseOffice.Typography.OpenType.Metrics;
using PulseOffice.Typography.Shaping;
using LayoutGlyphRun = PulseOffice.Documents.Layout.Tree.GlyphRun;

namespace PulseOffice.Documents.UI.Rendering;

public interface IGlyphRunPainter
{
    bool Paint(DrawingContext context, LayoutGlyphRun run, Point origin, IBrush ink);
}

public sealed record GlyphPiece(double X, IReadOnlyList<GlyphInfo> Glyphs, string? Missing);

public sealed record MappedGlyphs(IReadOnlyList<GlyphPiece> Pieces)
{
    public bool Complete => Pieces.All(piece => piece.Missing is null);

    public int Count => Pieces.Sum(piece => piece.Missing is null ? piece.Glyphs.Count : 1);
}

public interface IFaceGlyphMapping
{
    MappedGlyphs Map(string text, IReadOnlyList<double> advances, Func<int, int> glyphOf);

    MappedGlyphs Map(string text, IReadOnlyList<double> advances, FontMetricsFace face, double size);
}

public sealed class FaceGlyphMapping(ITextShaper shaper, IComplexScriptSegmenter segmenter) : IFaceGlyphMapping
{
    public FaceGlyphMapping() : this(new HarfBuzzTextShaper(), new ComplexScriptSegmenter())
    {
    }

    public MappedGlyphs Map(string text, IReadOnlyList<double> advances, Func<int, int> glyphOf) => Map(text, advances, glyphOf, new Dictionary<int, (ShapingSpan Span, IReadOnlyList<GlyphInfo> Glyphs)>());

    public MappedGlyphs Map(string text, IReadOnlyList<double> advances, FontMetricsFace face, double size)
    {
        ArgumentNullException.ThrowIfNull(face);
        var shaped = new Dictionary<int, (ShapingSpan Span, IReadOnlyList<GlyphInfo> Glyphs)>();
        if (size > 0 && double.IsFinite(size))
        {
            foreach (var span in segmenter.Spans(text))
            {
                var result = shaper.Shape(face, text, span);
                if (!result.Complete) continue;
                var width = 0.0;
                for (var i = span.Start; i < span.End && i < advances.Count; i++) width += advances[i];
                var glyphs = result.Glyphs
                    .Select(glyph => new GlyphInfo((ushort)glyph.Glyph, glyph.Cluster, glyph.Advance * size, new Vector(glyph.OffsetX * size, -glyph.OffsetY * size)))
                    .ToList();
                var natural = glyphs.Sum(glyph => glyph.GlyphAdvance);
                glyphs[^1] = new GlyphInfo(glyphs[^1].GlyphIndex, glyphs[^1].GlyphCluster, glyphs[^1].GlyphAdvance + width - natural, glyphs[^1].GlyphOffset);
                shaped[span.Start] = (span, glyphs);
            }
        }
        return Map(text, advances, face.GlyphOf, shaped);
    }

    private MappedGlyphs Map(string text, IReadOnlyList<double> advances, Func<int, int> glyphOf, IReadOnlyDictionary<int, (ShapingSpan Span, IReadOnlyList<GlyphInfo> Glyphs)> shaped)
    {
        var pieces = new List<GlyphPiece>();
        var glyphs = new List<GlyphInfo>();
        var start = 0.0;
        var x = 0.0;
        var lead = 0.0;

        void Close()
        {
            if (glyphs.Count > 0) pieces.Add(new GlyphPiece(start, [.. glyphs], null));
            glyphs.Clear();
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (shaped.TryGetValue(i, out var complex))
            {
                if (glyphs.Count == 0) start = pieces.Count == 0 ? lead : x;
                glyphs.AddRange(complex.Glyphs);
                for (var j = complex.Span.Start; j < complex.Span.End; j++) x += j < advances.Count ? advances[j] : 0;
                i = complex.Span.End - 1;
                continue;
            }
            var character = text[i];
            var advance = i < advances.Count ? advances[i] : 0;
            if (char.IsLowSurrogate(character) || character is '\u00AD' or '\t' or '\n' or '\u2028' or '\u200B')
            {
                if (glyphs.Count > 0) glyphs[^1] = new GlyphInfo(glyphs[^1].GlyphIndex, glyphs[^1].GlyphCluster, glyphs[^1].GlyphAdvance + advance);
                else if (pieces.Count == 0) lead += advance;
                x += advance;
                continue;
            }
            var pair = char.IsHighSurrogate(character) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
            var codePoint = pair ? char.ConvertToUtf32(character, text[i + 1]) : character;
            var glyph = glyphOf(codePoint);
            if (glyph == 0 && !char.IsWhiteSpace(character) && !char.IsControl(character))
            {
                Close();
                pieces.Add(new GlyphPiece(x, [], pair ? text.Substring(i, 2) : character.ToString()));
                x += advance;
                start = x;
                continue;
            }
            if (glyphs.Count == 0) start = pieces.Count == 0 ? lead : x;
            glyphs.Add(new GlyphInfo((ushort)glyph, i, advance));
            x += advance;
        }
        Close();
        return new MappedGlyphs(pieces);
    }
}

public sealed class FontFileRunPainter(IFolioFaces faces, IFaceGlyphMapping mapping) : IGlyphRunPainter
{
    public bool Paint(DrawingContext context, LayoutGlyphRun run, Point origin, IBrush ink)
    {
        if (faces.Resolve(run.Font.Family, run.Font.Bold, run.Font.Italic) is not { } face) return false;
        var mapped = mapping.Map(run.Text, run.Advances, face.Metrics, run.Font.Size);
        var fallback = new Typeface(FontFamily.Default, run.Font.Italic ? FontStyle.Italic : FontStyle.Normal, run.Font.Bold ? FontWeight.Bold : FontWeight.Normal);
        foreach (var piece in mapped.Pieces)
        {
            if (piece.Missing is { } missing)
            {
                var text = new FormattedText(missing, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, fallback, run.Font.Size, ink);
                context.DrawText(text, new Point(origin.X + piece.X, origin.Y - text.Baseline));
                continue;
            }
            var shaped = new Avalonia.Media.GlyphRun(face.Typeface, run.Font.Size, run.Text.AsMemory(), piece.Glyphs, new Point(origin.X + piece.X, origin.Y));
            context.DrawGlyphRun(ink, shaped);
        }
        return true;
    }
}

public sealed class FormattedRunPainter : IGlyphRunPainter
{
    public bool Paint(DrawingContext context, LayoutGlyphRun run, Point origin, IBrush ink)
    {
        var typeface = new Typeface(run.Font.Family, run.Font.Italic ? FontStyle.Italic : FontStyle.Normal, run.Font.Bold ? FontWeight.Bold : FontWeight.Normal);
        var x = origin.X;
        for (var i = 0; i < run.Text.Length; i++)
        {
            var advance = i < run.Advances.Count ? run.Advances[i] : 0;
            var character = run.Text[i];
            if (!char.IsControl(character) && !char.IsLowSurrogate(character) && character != '\u00AD')
            {
                var glyph = char.IsHighSurrogate(character) && i + 1 < run.Text.Length ? run.Text.Substring(i, 2) : character.ToString();
                var text = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, run.Font.Size, ink);
                context.DrawText(text, new Point(x, origin.Y - text.Baseline));
            }
            x += advance;
        }
        return true;
    }
}

public interface IRunPainting
{
    void Paint(DrawingContext context, LayoutGlyphRun run, Point origin, IBrush ink);
}

public sealed class RunPainting(IEnumerable<IGlyphRunPainter> painters) : IRunPainting
{
    private readonly IReadOnlyList<IGlyphRunPainter> _painters = [.. painters];

    public void Paint(DrawingContext context, LayoutGlyphRun run, Point origin, IBrush ink)
    {
        foreach (var painter in _painters)
        {
            if (painter.Paint(context, run, origin, ink)) return;
        }
    }
}

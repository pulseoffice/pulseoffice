using System.Globalization;
using System.Text;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Printing;
using PulseOffice.Typography.OpenType.Metrics;
using PulseOffice.Typography.Shaping;
using PulseOffice.Typography.Subsetting;

namespace PulseOffice.Formats.Pdf.Fonts;

public sealed record PdfShownText(IPdfFontResource Font, IReadOnlyList<PdfGlyph> Glyphs)
{
    public double Thousandths => Glyphs.Sum(glyph => glyph.Advance);

    public string? ActualText { get; init; }
}

public interface IPdfFontSet
{
    IReadOnlyList<PdfShownText> Show(string text, PrintFont font);

    IReadOnlyList<PdfShownText> Show(string text, ShapingSpan span, PrintFont font);

    IReadOnlyList<ShapingSpan> ComplexSpans(string text);

    double Measure(string text, PrintFont font);

    IReadOnlyList<IPdfFontResource> Resources { get; }

    IReadOnlyList<Diagnostic> Diagnostics { get; }
}

public interface IPdfFontSetFactory
{
    IPdfFontSet Create();
}

public interface IPdfFaceLocator
{
    FontMetricsFace? Requested(string family, bool bold, bool italic);

    FontMetricsFace? Exact(string family, bool bold, bool italic);
}

public sealed class NoFaceLocator : IPdfFaceLocator
{
    public FontMetricsFace? Requested(string family, bool bold, bool italic) => null;

    public FontMetricsFace? Exact(string family, bool bold, bool italic) => null;
}

public sealed class PdfFaceLocator(IFontFileLocator requested, IFontFileLocator installed) : IPdfFaceLocator
{
    public FontMetricsFace? Requested(string family, bool bold, bool italic) =>
        requested.Find(family, bold, italic) ?? installed.Find(family, bold, italic);

    public FontMetricsFace? Exact(string family, bool bold, bool italic) => installed.Find(family, bold, italic);
}

public interface IPdfFallbackFonts
{
    IReadOnlyList<string> Families { get; }
}

public sealed class PdfFallbackFonts : IPdfFallbackFonts
{
    public IReadOnlyList<string> Families { get; } =
    [
        "Noto Sans", "DejaVu Sans", "Arial Unicode MS", "Segoe UI", "Helvetica Neue", "Arial", "Lucida Grande", "Liberation Sans",
        "Noto Sans CJK SC", "Noto Sans CJK JP", "Noto Sans CJK KR", "Source Han Sans", "PingFang SC", "Hiragino Sans GB", "STHeiti",
        "Heiti SC", "Microsoft YaHei", "Yu Gothic", "MS Gothic", "Malgun Gothic", "Apple SD Gothic Neo", "AppleGothic",
        "Noto Sans Hebrew", "Noto Sans Arabic", "Geeza Pro", "Arial Hebrew", "Noto Sans Devanagari", "Kohinoor Devanagari", "Thonburi",
        "Noto Sans Symbols", "Noto Sans Symbols 2", "Noto Sans Math", "STIX Two Math", "Cambria Math", "Apple Symbols",
        "Segoe UI Symbol", "Segoe UI Emoji", "Noto Emoji", "OpenSymbol", "Symbola", "DejaVu Serif", "FreeSans", "FreeSerif",
    ];
}

public sealed record PdfFontOptions
{
    public long MaximumWholeFontBytes { get; init; } = 16 * 1024 * 1024;
}

public sealed record PdfShaping(ITextShaper Shaper, IComplexScriptSegmenter Segmenter);

public sealed class PdfFontSetFactory(IPdfFaceLocator faces, IPdfFallbackFonts fallbacks, ITrueTypeSubsetter subsetter, IToUnicodeWriter unicode, IPdfFontMetrics standard, ITextShaper shaper, IComplexScriptSegmenter segmenter) : IPdfFontSetFactory
{
    public PdfFontOptions Options { get; init; } = new();

    public IPdfFontSet Create() => new PdfFontSet(faces, fallbacks, subsetter, unicode, standard, Options, new PdfShaping(shaper, segmenter));
}

public sealed class StandardPdfFonts(IPdfFontMetrics standard) : IPdfFontSetFactory
{
    public IPdfFontSet Create() =>
        new PdfFontSet(new NoFaceLocator(), new PdfFallbackFonts(), new TrueTypeSubsetter(), new ToUnicodeWriter(), standard, new PdfFontOptions(),
            new PdfShaping(new HarfBuzzTextShaper(), new ComplexScriptSegmenter()));
}

public sealed class PdfFontSet(IPdfFaceLocator faces, IPdfFallbackFonts fallbacks, ITrueTypeSubsetter subsetter, IToUnicodeWriter unicode, IPdfFontMetrics standard, PdfFontOptions options, PdfShaping shaping) : IPdfFontSet
{
    private readonly List<IPdfFontResource> _resources = [];
    private readonly Dictionary<FontMetricsFace, IPdfFontResource?> _embedded = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<PdfStandardFont, StandardFontResource> _standard = [];
    private readonly Dictionary<(string, bool, bool), IPdfFontResource?> _primary = [];
    private readonly Dictionary<(string, bool, bool, int), IPdfFontResource> _chosen = [];
    private readonly Dictionary<(bool, bool), IReadOnlyList<IPdfFontResource>> _fallbacks = [];
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly HashSet<int> _missing = [];
    private readonly HashSet<string> _refused = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Text, PrintFont Font), IReadOnlyList<PdfShownText>> _clusters = [];
    private const int CachedClusterLength = 8;
    private const int CachedClusters = 65536;

    public IReadOnlyList<IPdfFontResource> Resources => _resources;

    public IReadOnlyList<Diagnostic> Diagnostics
    {
        get
        {
            var all = new List<Diagnostic>(_diagnostics);
            if (_missing.Count > 0)
            {
                var list = string.Join(", ", _missing.Order().Take(24).Select(code => "U+" + code.ToString("X4", CultureInfo.InvariantCulture)));
                all.Add(Diagnostic.Warning("PDF012", $"No installed font with outlines covers {_missing.Count} character(s): {list}.").With(_missing.Count, list));
            }
            return all;
        }
    }

    public IReadOnlyList<ShapingSpan> ComplexSpans(string text) => shaping.Segmenter.Spans(text ?? string.Empty);

    public double Measure(string text, PrintFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (string.IsNullOrEmpty(text)) return 0;
        text = Normalized(text);
        var total = 0.0;
        var position = 0;
        foreach (var span in ComplexSpans(text))
        {
            total += Plain(text[position..span.Start], font);
            total += Shaped(text, span, font) is { } shaped ? shaped.Text.Width * 1000 : Plain(text.Substring(span.Start, span.Length), font);
            position = span.End;
        }
        total += Plain(text[position..], font);
        return total * font.SizePoints / 1000.0;
    }

    public IReadOnlyList<PdfShownText> Show(string text, PrintFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (string.IsNullOrEmpty(text)) return [];
        if (text.Length > CachedClusterLength) return Composed(text, font);
        if (_clusters.TryGetValue((text, font), out var cached)) return cached;
        var composed = Composed(text, font);
        if (_clusters.Count < CachedClusters) _clusters[(text, font)] = composed;
        return composed;
    }

    private IReadOnlyList<PdfShownText> Composed(string text, PrintFont font)
    {
        var shown = new List<PdfShownText>();
        text = Normalized(text);
        var position = 0;
        foreach (var span in ComplexSpans(text))
        {
            shown.AddRange(ShowPlain(text[position..span.Start], font));
            shown.AddRange(Show(text, span, font));
            position = span.End;
        }
        shown.AddRange(ShowPlain(text[position..], font));
        return shown;
    }

    public IReadOnlyList<PdfShownText> Show(string text, ShapingSpan span, PrintFont font)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        if (span.Start < 0 || span.Length <= 0 || span.End > text.Length) return [];
        var original = text.Substring(span.Start, span.Length);
        if (Shaped(text, span, font) is not { } shaped) return ShowPlain(original, font);

        var glyphs = new List<PdfGlyph>(shaped.Text.Glyphs.Count);
        var starts = shaped.Text.Glyphs.Select(glyph => glyph.Cluster).Distinct().Order().ToList();
        var named = new HashSet<int>();
        foreach (var glyph in shaped.Text.Glyphs)
        {
            var next = starts.FirstOrDefault(start => start > glyph.Cluster, span.End);
            var cluster = Math.Clamp(glyph.Cluster, span.Start, span.End);
            var clusterText = named.Add(glyph.Cluster) ? text[cluster..Math.Clamp(next, cluster, span.End)] : null;
            var used = shaped.Font.UseGlyph(glyph.Glyph, clusterText);
            glyphs.Add(used with { Advance = glyph.Advance * 1000, OffsetX = glyph.OffsetX * 1000, Rise = glyph.OffsetY * 1000 });
        }
        return [new PdfShownText(shaped.Font, glyphs) { ActualText = original }];
    }

    private (IPdfFontResource Font, ShapedText Text)? Shaped(string text, ShapingSpan span, PrintFont font)
    {
        var strong = text.Substring(span.Start, span.Length).EnumerateRunes().FirstOrDefault(rune => !Invisible(rune.Value)
            && Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpaceSeparator or UnicodeCategory.EnclosingMark));
        if (strong.Value == 0) return null;
        var resource = Resolve(font, strong.Value);
        if (resource.Face is not { } face) return null;
        var shaped = shaping.Shaper.Shape(face, text, span);
        return shaped.Complete ? (resource, shaped) : null;
    }

    private double Plain(string text, PrintFont font)
    {
        var total = 0.0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (Skipped(rune.Value, font)) continue;
            total += Resolve(font, rune.Value).Advance(Substitute(rune.Value, font));
        }
        return total;
    }

    private List<PdfShownText> ShowPlain(string text, PrintFont font)
    {
        var shown = new List<PdfShownText>();
        IPdfFontResource? current = null;
        var glyphs = new List<PdfGlyph>();
        foreach (var rune in text.EnumerateRunes())
        {
            if (Skipped(rune.Value, font)) continue;
            var resource = Resolve(font, rune.Value);
            if (!ReferenceEquals(resource, current) && glyphs.Count > 0)
            {
                shown.Add(new PdfShownText(current!, glyphs));
                glyphs = [];
            }
            current = resource;
            glyphs.Add(resource.Use(Substitute(rune.Value, font)));
        }
        if (current is not null && glyphs.Count > 0) shown.Add(new PdfShownText(current, glyphs));
        return shown;
    }

    private string Normalized(string text) => text.IsNormalized(NormalizationForm.FormC) ? text : text.Normalize(NormalizationForm.FormC);

    private bool Skipped(int codePoint, PrintFont font) => Invisible(codePoint);

    private bool Invisible(int codePoint) =>
        codePoint is 0x00AD or 0x200B or 0x200C or 0x200D or 0x2060 or 0xFEFF or (>= 0xFE00 and <= 0xFE0F) or (>= 0xE0100 and <= 0xE01EF)
        || (codePoint < 0x20 && codePoint != '\t');

    private int Substitute(int codePoint, PrintFont font) => codePoint == '\t' ? ' ' : codePoint;

    private IPdfFontResource Resolve(PrintFont font, int codePoint)
    {
        codePoint = Substitute(codePoint, font);
        var key = (font.Family, font.Bold, font.Italic, codePoint);
        if (_chosen.TryGetValue(key, out var chosen)) return chosen;
        chosen = Primary(font) is { } primary && primary.Covers(codePoint)
            ? primary
            : Fallbacks(font).FirstOrDefault(candidate => candidate.Covers(codePoint)) ?? Standard(font, codePoint);
        _chosen[key] = chosen;
        return chosen;
    }

    private IPdfFontResource? Primary(PrintFont font)
    {
        var key = (font.Family, font.Bold, font.Italic);
        if (_primary.TryGetValue(key, out var found)) return found;
        found = faces.Requested(font.Family, font.Bold, font.Italic) is { } face ? Embedded(face, font.Bold, font.Italic, announce: true) : null;
        _primary[key] = found;
        return found;
    }

    private IReadOnlyList<IPdfFontResource> Fallbacks(PrintFont font)
    {
        var key = (font.Bold, font.Italic);
        if (_fallbacks.TryGetValue(key, out var list)) return list;
        list = [.. fallbacks.Families
            .Select(family => faces.Exact(family, font.Bold, font.Italic))
            .OfType<FontMetricsFace>()
            .Distinct(ReferenceEqualityComparer.Instance).Cast<FontMetricsFace>()
            .Select(face => Embedded(face, font.Bold, font.Italic, announce: false))
            .OfType<IPdfFontResource>()];
        _fallbacks[key] = list;
        return list;
    }

    private IPdfFontResource? Embedded(FontMetricsFace face, bool bold, bool italic, bool announce)
    {
        if (_embedded.TryGetValue(face, out var resource)) return resource;
        resource = Mode(face, announce) is { } mode
            ? Register(new EmbeddedFontResource($"F{_resources.Count + 1}", face, mode, bold, italic, subsetter, unicode))
            : null;
        _embedded[face] = resource;
        return resource;
    }

    private EmbeddingMode? Mode(FontMetricsFace face, bool announce)
    {
        var permission = face.Face.Embedding;
        if (!permission.AllowsEmbedding)
        {
            if (announce) Refuse(face, "PDF010", $"The font '{face.Family}' forbids embedding (fsType {face.Face.FsType:X4}); another font is used.");
            return null;
        }
        if (face.HasTrueTypeOutlines) return permission.NoSubsetting ? EmbeddingMode.TrueTypeWhole : EmbeddingMode.TrueTypeSubset;
        if (face.Tables.Has("CFF ") && face.Tables.Size <= options.MaximumWholeFontBytes) return EmbeddingMode.OpenTypeWhole;
        if (announce) Refuse(face, "PDF011", $"The font '{face.Family}' has no outlines that can be embedded; another font is used.");
        return null;
    }

    private void Refuse(FontMetricsFace face, string code, string message)
    {
        if (_refused.Add(code + face.Family)) _diagnostics.Add(Diagnostic.Warning(code, message).With(face.Family));
    }

    private IPdfFontResource Standard(PrintFont font, int codePoint)
    {
        var resolved = standard.Resolve(font.Family, font.Bold, font.Italic);
        if (!_standard.TryGetValue(resolved, out var resource))
        {
            resource = new StandardFontResource($"F{_resources.Count + 1}", resolved, standard);
            _standard[resolved] = resource;
            Register(resource);
        }
        if (!resource.Covers(codePoint) && codePoint > ' ') _missing.Add(codePoint);
        return resource;
    }

    private T Register<T>(T resource) where T : IPdfFontResource
    {
        _resources.Add(resource);
        return resource;
    }
}

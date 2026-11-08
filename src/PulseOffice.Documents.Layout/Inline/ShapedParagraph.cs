using PulseOffice.Documents.Layout.Measurement;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Inline;

public sealed record ShapedRun(TextRun Style, FontKey Font, double Shift, FontMetrics Metrics, int Start, int End, double HyphenWidth);

public sealed record ShapedLabel(string Text, FontKey Font, FontMetrics Metrics, IReadOnlyList<double> Advances, double Position)
{
    public double Width => Advances.Sum();

    public bool FollowedByTab { get; init; } = true;

    public PulseOffice.Text.Rich.ListBulletPicture? Picture { get; init; }
}

public sealed record DropShape(int Length, FontKey Font, IReadOnlyList<double> Advances, double Indent, int Lines, double Lowering)
{
    public bool Covers(int lineIndex) => lineIndex < Lines;
}

public sealed record ParagraphFrame(double MarginLeft, double MarginRight, double FirstLineIndent, TextAlign Align, IReadOnlyList<TabStop> Tabs, double TabInterval)
{
    public double TabOrigin { get; init; }

    public bool RightToLeft { get; init; }

    public double LeftEdge => RightToLeft ? MarginRight : MarginLeft;

    public double RightEdge => RightToLeft ? MarginLeft : MarginRight;

    public ParagraphFrame Narrowed(double left, double right) => RightToLeft
        ? this with { MarginLeft = Math.Max(0, MarginLeft - right), MarginRight = Math.Max(0, MarginRight - left) }
        : this with { MarginLeft = Math.Max(0, MarginLeft - left), MarginRight = Math.Max(0, MarginRight - right) };
}

public sealed class ShapedParagraph(
    string text,
    double[] advances,
    int[] runOf,
    IReadOnlyList<ShapedRun> runs,
    FontMetrics fallback,
    ShapedLabel? label,
    IReadOnlySet<int> hyphenPoints)
{
    public string Text { get; } = text;

    public double[] Advances { get; } = advances;

    public int[] RunOf { get; } = runOf;

    public IReadOnlyList<ShapedRun> Runs { get; } = runs;

    public FontMetrics Fallback { get; } = fallback;

    public ShapedLabel? Label { get; } = label;

    public IReadOnlySet<int> HyphenPoints { get; } = hyphenPoints;

    public ShapedRun RunAt(int index) => Runs[RunOf.Length == 0 ? 0 : RunOf[Math.Clamp(index, 0, RunOf.Length - 1)]];

    public int[]? SourceMap { get; init; }

    public DropShape? Drop { get; init; }

    public PulseOffice.Text.Rich.Bidi.TextDirection? Direction { get; init; }

    public IReadOnlyDictionary<int, InlineGlyph> Inlines { get; init; } = new Dictionary<int, InlineGlyph>();

    public int SourceOf(int index) => SourceMap is null ? index : SourceMap[Math.Clamp(index, 0, SourceMap.Length - 1)];
}

public enum InlineKind
{
    Object,
    Citation,
    Deleted,
}

public sealed record InlineInsertion(int Offset, InlineKind Kind, string Id, string Text, double Width, double Height)
{
    public uint? Ink { get; init; }

    public bool Underlined { get; init; }
}

public sealed record InlineGlyph(InlineKind Kind, string Id, string Text, FontKey Font, IReadOnlyList<double> Advances, double Ascent, double Descent, double Shift)
{
    public TextRun? Style { get; init; }
}

public interface IRunFonts
{
    FontKey FontOf(TextRun run, string defaultFamily);

    double ShiftOf(TextRun run);
}

public sealed class RunFonts(Flow.LayoutOptions options) : IRunFonts
{
    public FontKey FontOf(TextRun run, string defaultFamily)
    {
        var size = run.FontSize ?? 12;
        if (run.Baseline != TextBaseline.Normal) size *= options.SuperscriptScale;
        return new FontKey(string.IsNullOrEmpty(run.Font) ? defaultFamily : run.Font!, size, run.Bold, run.Italic) { Kerning = run.Kerning ?? true };
    }

    public double ShiftOf(TextRun run)
    {
        var size = run.FontSize ?? 12;
        return run.Baseline switch
        {
            TextBaseline.Superscript => size * options.SuperscriptRaise,
            TextBaseline.Subscript => -size * options.SubscriptLower,
            _ => 0,
        };
    }
}

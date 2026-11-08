using PulseOffice.Documents.Layout.Measurement;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Inline;

public interface IInlineInserter
{
    ShapedParagraph Insert(ShapedParagraph paragraph, IReadOnlyList<InlineInsertion> insertions);
}

public sealed class InlineInserter(ITextMeasurer measurer, IRunFonts fonts) : IInlineInserter
{
    public const char Placeholder = '￼';

    public ShapedParagraph Insert(ShapedParagraph paragraph, IReadOnlyList<InlineInsertion> insertions)
    {
        if (insertions.Count == 0) return paragraph;
        var ordered = insertions.OrderBy(item => item.Offset).ToList();
        var length = paragraph.Text.Length;
        var text = new System.Text.StringBuilder(length + ordered.Count);
        var advances = new List<double>(length + ordered.Count);
        var runOf = new List<int>(length + ordered.Count);
        var map = new List<int>(length + ordered.Count + 1);
        var inlines = new Dictionary<int, InlineGlyph>();
        var shifted = new int[length + 1];
        var next = 0;
        for (var source = 0; source <= length; source++)
        {
            while (next < ordered.Count && Math.Clamp(ordered[next].Offset, 0, length) == source)
            {
                var item = ordered[next++];
                var runIndex = paragraph.RunOf.Length == 0 ? 0 : paragraph.RunOf[Math.Clamp(source - 1, 0, paragraph.RunOf.Length - 1)];
                var glyph = Glyph(item, paragraph.Runs[runIndex]);
                inlines[text.Length] = glyph;
                text.Append(Placeholder);
                advances.Add(glyph.Advances.Sum());
                runOf.Add(runIndex);
                map.Add(source);
            }
            shifted[source] = text.Length;
            if (source == length) break;
            text.Append(paragraph.Text[source]);
            advances.Add(paragraph.Advances[source]);
            runOf.Add(paragraph.RunOf[source]);
            map.Add(source);
        }
        map.Add(length);
        var runs = paragraph.Runs.Select(run => run with { Start = run.Start == 0 ? 0 : shifted[run.Start], End = run.End >= length ? text.Length : shifted[run.End] }).ToList();
        var points = paragraph.HyphenPoints.Select(point => shifted[Math.Clamp(point, 0, length)]).ToHashSet();
        return new ShapedParagraph(text.ToString(), [.. advances], [.. runOf], runs, paragraph.Fallback, paragraph.Label, points)
        {
            SourceMap = [.. map],
            Inlines = inlines,
            Drop = paragraph.Drop,
            Direction = paragraph.Direction,
        };
    }

    private InlineGlyph Glyph(InlineInsertion item, ShapedRun run)
    {
        if (item.Kind == InlineKind.Object)
        {
            return new InlineGlyph(item.Kind, item.Id, string.Empty, run.Font, [item.Width], item.Height, 0, 0);
        }
        if (item.Kind == InlineKind.Deleted)
        {
            var struck = run.Style with { Strikethrough = true, Underline = item.Underlined, Color = item.Ink ?? run.Style.Color, FontSize = run.Style.FontSize ?? run.Font.Size };
            var face = fonts.FontOf(struck, run.Font.Family);
            var measured = measurer.Metrics(face);
            return new InlineGlyph(item.Kind, item.Id, item.Text, face, measurer.Advances(face, item.Text), measured.Ascent + measured.LineGap, measured.Descent, fonts.ShiftOf(struck)) { Style = struck };
        }
        var style = run.Style with { Baseline = TextBaseline.Superscript, FontSize = run.Style.FontSize ?? run.Font.Size };
        var font = fonts.FontOf(style, run.Font.Family);
        var metrics = measurer.Metrics(font);
        return new InlineGlyph(item.Kind, item.Id, item.Text, font, measurer.Advances(font, item.Text), metrics.Ascent + metrics.LineGap, metrics.Descent, fonts.ShiftOf(style));
    }
}

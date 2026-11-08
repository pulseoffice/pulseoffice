using PulseOffice.Documents.Layout.Inline;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Lines;

public sealed record CompositionRequest(string ParagraphId, ShapedParagraph Paragraph, ParagraphFrame Frame, LineSpacingRule Spacing, double ColumnWidth);

public interface ILineComposer
{
    ComposedLine Compose(CompositionRequest request, LineSpan span, int index);
}

public sealed class LineComposer(ILineBidi bidi) : ILineComposer
{
    public ComposedLine Compose(CompositionRequest request, LineSpan span, int index)
    {
        var paragraph = request.Paragraph;
        var frame = request.Frame;
        var available = request.ColumnWidth - frame.MarginLeft - frame.MarginRight;
        var hyphen = span.Hyphenated ? paragraph.RunAt(span.End - 1).HyphenWidth : 0;
        var right = span.X0 + span.ContentWidth + hyphen;
        var free = available - right;
        var widths = span.Widths.ToArray();
        var directions = bidi.Resolve(paragraph, span);
        var offset = frame.Align switch
        {
            TextAlign.Right => free,
            TextAlign.Center => free / 2,
            _ => 0,
        };
        if (frame.Align == TextAlign.Justify && !span.IsLast && free > 0) Justify(paragraph.Text, span, widths, free);
        var runs = new List<GlyphRun>();
        if (index == 0 && paragraph.Label is { } label)
        {
            runs.Add(new GlyphRun(GlyphRunKind.Label, label.Font, label.Text, frame.MarginLeft + label.Position + Math.Max(0, offset), 0, paragraph.SourceOf(span.Start), label.Advances) { Picture = label.Picture });
        }
        var x = frame.MarginLeft + span.X0 + Math.Max(0, offset);
        var origin = x;
        var dropped = 0;
        if (span.Start == 0 && paragraph.Drop is { } drop)
        {
            dropped = drop.Length;
            var cap = paragraph.RunAt(0);
            runs.Add(new GlyphRun(GlyphRunKind.Text, drop.Font, paragraph.Text[..drop.Length], frame.MarginLeft + frame.FirstLineIndent, -drop.Lowering, paragraph.SourceOf(0), drop.Advances) { Style = cap.Style });
        }
        var fixedPrefix = runs.Count;
        AddTextRuns(paragraph, span, widths, runs, ref x, dropped, directions);
        if (span.Hyphenated)
        {
            var run = paragraph.RunAt(span.End - 1);
            runs.Add(new GlyphRun(GlyphRunKind.Hyphen, run.Font, "-", x, run.Shift, paragraph.SourceOf(span.End), [hyphen]) { Style = run.Style, Level = directions?.BaseLevel ?? 0 });
        }
        IReadOnlyList<GlyphRun> arranged = directions is null ? runs : bidi.Arrange(runs, fixedPrefix, directions, request.ColumnWidth);
        var (ascent, descent) = Extent(paragraph, span, index);
        var geometry = request.Spacing.Apply(ascent, descent);
        var height = geometry.Height;
        if (span.IsLast && paragraph.Drop is { } below && below.Covers(index + 1)) height += (below.Lines - 1 - index) * geometry.Height;
        return new ComposedLine(request.ParagraphId, index, paragraph.SourceOf(span.Start), paragraph.SourceOf(span.End), request.ColumnWidth, height, geometry.Ascent, height - geometry.Ascent, arranged)
        {
            IsLast = span.IsLast,
            Origin = directions is { RightToLeft: true } ? request.ColumnWidth - origin : origin,
            RightToLeft = directions?.RightToLeft ?? false,
            Bidirectional = directions is not null,
            Forced = span.Forced,
            Broken = span.Broken,
            Next = span.End,
            TextHeight = height > geometry.Height ? geometry.Height : null,
        };
    }

    private void Justify(string text, LineSpan span, double[] widths, double free)
    {
        var lastTab = -1;
        for (var i = span.Start; i < span.ContentEnd; i++) if (text[i] == '\t') lastTab = i;
        var spaces = new List<int>();
        for (var i = Math.Max(span.Start, lastTab + 1); i < span.ContentEnd; i++) if (text[i] == ' ') spaces.Add(i - span.Start);
        if (spaces.Count == 0) return;
        var extra = free / spaces.Count;
        foreach (var space in spaces) widths[space] += extra;
    }

    private void AddTextRuns(ShapedParagraph paragraph, LineSpan span, double[] widths, List<GlyphRun> runs, ref double x, int skip, LineDirections? directions)
    {
        var text = paragraph.Text;
        var segment = Math.Max(span.Start, Math.Min(skip, span.End));
        for (var i = segment; i <= span.End; i++)
        {
            var inline = i < span.End && paragraph.Inlines.ContainsKey(i);
            var boundary = i == span.End || text[i] == '\t' || text[i] is '\n' or '\u2028' || inline
                || (i > segment && paragraph.RunOf[i] != paragraph.RunOf[segment])
                || (i > segment && directions is not null && directions.StartsAt(i));
            if (!boundary) continue;
            if (i > segment) x = AddRun(paragraph, span, widths, runs, segment, i, x, GlyphRunKind.Text, directions);
            if (i < span.End && text[i] == '\t') x = AddRun(paragraph, span, widths, runs, i, i + 1, x, GlyphRunKind.Tab, directions);
            if (inline) x = AddInline(paragraph, paragraph.Inlines[i], widths[i - span.Start], runs, i, x, directions);
            segment = i < span.End && (text[i] == '\t' || text[i] is '\n' or '\u2028' || inline) ? i + 1 : i;
        }
    }

    private double AddRun(ShapedParagraph paragraph, LineSpan span, double[] widths, List<GlyphRun> runs, int from, int to, double x, GlyphRunKind kind, LineDirections? directions)
    {
        var run = paragraph.RunAt(from);
        var advances = new double[to - from];
        Array.Copy(widths, from - span.Start, advances, 0, advances.Length);
        runs.Add(new GlyphRun(kind, run.Font, paragraph.Text[from..to], x, run.Shift, paragraph.SourceOf(from), advances)
        {
            Style = run.Style,
            Leader = kind == GlyphRunKind.Tab ? span.Leaders.GetValueOrDefault(from) : TabLeader.None,
            Level = directions?.LevelAt(from) ?? 0,
        });
        return x + advances.Sum();
    }

    private double AddInline(ShapedParagraph paragraph, InlineGlyph glyph, double width, List<GlyphRun> runs, int at, double x, LineDirections? directions)
    {
        var kind = glyph.Kind == InlineKind.Object ? GlyphRunKind.Object : GlyphRunKind.Citation;
        var advances = glyph.Kind == InlineKind.Object ? (IReadOnlyList<double>)[width] : glyph.Advances;
        runs.Add(new GlyphRun(kind, glyph.Font, glyph.Text, x, glyph.Shift, paragraph.SourceOf(at), advances)
        {
            Style = glyph.Style ?? paragraph.RunAt(at).Style,
            ObjectId = glyph.Kind == InlineKind.Object ? glyph.Id : null,
            NoteId = glyph.Kind == InlineKind.Citation ? glyph.Id : null,
            Ascent = glyph.Ascent,
            Level = directions?.LevelAt(at) ?? 0,
        });
        return x + width;
    }

    private (double Ascent, double Descent) Extent(ShapedParagraph paragraph, LineSpan span, int index)
    {
        var ascent = 0.0;
        var descent = 0.0;
        var touched = false;
        foreach (var run in RunsWithin(paragraph, span))
        {
            touched = true;
            ascent = Math.Max(ascent, run.Metrics.Ascent + run.Metrics.LineGap + run.Shift);
            descent = Math.Max(descent, run.Metrics.Descent - run.Shift);
        }
        if (!touched)
        {
            var run = paragraph.Text.Length == 0 ? paragraph.Runs[0] : paragraph.RunAt(Math.Max(0, span.Start - 1));
            ascent = run.Metrics.Ascent + run.Metrics.LineGap;
            descent = run.Metrics.Descent;
        }
        foreach (var glyph in InlinesWithin(paragraph, span))
        {
            ascent = Math.Max(ascent, glyph.Ascent + glyph.Shift);
            descent = Math.Max(descent, glyph.Descent - glyph.Shift);
        }
        if (index == 0 && paragraph.Label is { } label)
        {
            ascent = Math.Max(ascent, label.Metrics.Ascent + label.Metrics.LineGap);
            descent = Math.Max(descent, label.Metrics.Descent);
        }
        return (ascent, descent);
    }

    private IEnumerable<ShapedRun> RunsWithin(ShapedParagraph paragraph, LineSpan span)
    {
        var runs = paragraph.Runs;
        var low = 0;
        var high = runs.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (runs[middle].End <= span.Start) low = middle + 1;
            else high = middle;
        }
        for (var index = low; index < runs.Count && runs[index].Start < span.End; index++)
        {
            if (runs[index].End > span.Start) yield return runs[index];
        }
    }

    private IEnumerable<InlineGlyph> InlinesWithin(ShapedParagraph paragraph, LineSpan span)
    {
        if (paragraph.Inlines.Count == 0) yield break;
        for (var position = span.Start; position < span.End; position++)
        {
            if (paragraph.Inlines.TryGetValue(position, out var glyph)) yield return glyph;
        }
    }
}

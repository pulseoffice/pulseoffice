namespace PulseOffice.Text.Rich;

public interface IRichTextInsertion
{
    RichText Insert(RichText text, (int Start, int End)? range, IReadOnlyList<TextParagraph> fragment);
}

public sealed class RichTextInsertion : IRichTextInsertion
{
    private readonly RunMerger _merger = new();

    public RichText Insert(RichText text, (int Start, int End)? range, IReadOnlyList<TextParagraph> fragment)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fragment);
        if (fragment.Count == 0) return text;

        var paragraphs = text.Paragraphs.Count > 0 ? text.Paragraphs : [new TextParagraph(string.Empty)];
        var length = text.Paragraphs.Count > 0 ? text.PlainText.Length : 0;
        var (start, end) = range ?? (length, length);
        start = Math.Clamp(Math.Min(start, end), 0, length);
        end = Math.Clamp(Math.Max(start, end), start, length);

        var (first, firstOffset) = Locate(paragraphs, start);
        var (last, lastOffset) = Locate(paragraphs, end);
        var left = Slice(paragraphs[first], 0, firstOffset);
        var right = Slice(paragraphs[last], lastOffset, paragraphs[last].Text.Length);

        var result = new List<TextParagraph>(paragraphs.Count + fragment.Count);
        result.AddRange(paragraphs.Take(first));
        if (fragment.Count == 1)
        {
            result.Add(paragraphs[first].WithRuns(Merge([.. left, .. fragment[0].EffectiveRuns(), .. right])));
        }
        else
        {
            result.Add(paragraphs[first].WithRuns(Merge([.. left, .. fragment[0].EffectiveRuns()])));
            result.AddRange(fragment.Skip(1).Take(fragment.Count - 2).Select(paragraph => paragraph.WithRuns(paragraph.EffectiveRuns())));
            result.Add(fragment[^1].WithRuns(Merge([.. fragment[^1].EffectiveRuns(), .. right])));
        }
        result.AddRange(paragraphs.Skip(last + 1));

        return text with { Paragraphs = result };
    }

    private (int Paragraph, int Offset) Locate(IReadOnlyList<TextParagraph> paragraphs, int offset)
    {
        var consumed = 0;
        for (var index = 0; index < paragraphs.Count; index++)
        {
            var length = paragraphs[index].Text.Length;
            if (offset <= consumed + length) return (index, offset - consumed);
            consumed += length + 1;
        }
        return (paragraphs.Count - 1, paragraphs[^1].Text.Length);
    }

    private List<TextRun> Slice(TextParagraph paragraph, int from, int to)
    {
        var runs = new List<TextRun>();
        var position = 0;
        foreach (var run in paragraph.EffectiveRuns())
        {
            var runStart = position;
            var runEnd = position + run.Text.Length;
            position = runEnd;
            var start = Math.Max(runStart, from);
            var end = Math.Min(runEnd, to);
            if (end <= start) continue;
            var whole = start == runStart && end == runEnd;
            var piece = run with { Text = run.Text[(start - runStart)..(end - runStart)] };
            runs.Add(whole || run.Field == TextFieldKind.None ? piece : piece with { Field = TextFieldKind.None });
        }
        return runs;
    }

    private List<TextRun> Merge(IReadOnlyList<TextRun> runs) =>
        _merger.Merge(runs.Where(run => run.Text.Length > 0 || run.Field != TextFieldKind.None));
}

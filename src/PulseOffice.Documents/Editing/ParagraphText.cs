using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing;

public interface IParagraphText
{
    IReadOnlyList<TextRun> Runs(TextParagraph paragraph);

    ParagraphState Insert(ParagraphState state, int offset, string text);

    ParagraphState Delete(ParagraphState state, int start, int end);

    (ParagraphState Left, ParagraphState Right) Split(ParagraphState state, int offset);

    ParagraphState Concat(ParagraphState left, ParagraphState right);

    ParagraphState Map(ParagraphState state, int start, int end, Func<TextRun, TextRun> map);

    ParagraphState AddMark(ParagraphState state, InlineMark mark);

    ParagraphState Restyle(ParagraphState state, int start, int end, IReadOnlyList<TextRun> runs);

    IReadOnlyList<TextRun> Slice(ParagraphState state, int start, int end);

    IReadOnlyList<InlineMark> MarksRemovedBy(ParagraphState state, int start, int end);

    int Snap(ParagraphState state, int offset);

    (int Start, int End) Atomic(ParagraphState state, int start, int end);
}

public sealed class ParagraphText : IParagraphText
{
    public IReadOnlyList<TextRun> Runs(TextParagraph paragraph) =>
        paragraph.Runs.Count > 0 ? paragraph.Runs : paragraph.Text.Length > 0 ? [new TextRun(paragraph.Text)] : [];

    public ParagraphState Insert(ParagraphState state, int offset, string text)
    {
        Check(state, offset);
        if (text.Length == 0) return state;
        offset = Snap(state, offset);
        var runs = Runs(state.Content);
        var template = Template(runs, offset);
        var (before, after) = Cut(runs, offset);
        List<TextRun> merged = [.. before, template with { Text = text }, .. after];
        var marks = state.Marks.Select(mark => ShiftForInsert(mark, offset, text.Length)).ToList();
        return new ParagraphState(state.Content.WithRuns(Normalize(merged)), marks);
    }

    public ParagraphState Delete(ParagraphState state, int start, int end)
    {
        Check(state, start);
        Check(state, end);
        if (end <= start) return state;
        (start, end) = Atomic(state, start, end);
        var runs = Runs(state.Content);
        var (before, rest) = Cut(runs, start);
        var (_, after) = Cut(rest, end - start);
        var removed = MarksRemovedBy(state, start, end).ToHashSet<InlineMark>(ReferenceEqualityComparer.Instance);
        var marks = state.Marks
            .Where(mark => !removed.Contains(mark))
            .Select(mark => mark.Offset <= start ? mark : mark.At(mark.Offset >= end ? mark.Offset - (end - start) : start))
            .ToList();
        var content = state.Content.WithRuns(Normalize([.. before, .. after]));
        return new ParagraphState(content, marks);
    }

    public (ParagraphState Left, ParagraphState Right) Split(ParagraphState state, int offset)
    {
        Check(state, offset);
        offset = Snap(state, offset);
        var (before, after) = Cut(Runs(state.Content), offset);
        var collapsed = state.Marks.Where(mark => mark.Offset == offset && mark.Span == MarkEdge.End).Select(mark => mark.Key).ToHashSet(StringComparer.Ordinal);
        var leftMarks = state.Marks.Where(mark => StaysLeft(mark, offset, collapsed)).ToList();
        var rightMarks = state.Marks.Where(mark => !StaysLeft(mark, offset, collapsed)).Select(mark => mark.At(mark.Offset - offset)).ToList();
        var left = new ParagraphState(state.Content.WithRuns(Normalize(before)), leftMarks);
        var right = new ParagraphState(state.Content.WithRuns(Normalize(after)), rightMarks);
        return (left, right);
    }

    public ParagraphState Concat(ParagraphState left, ParagraphState right)
    {
        var length = left.Length;
        var runs = Normalize([.. Runs(left.Content), .. Runs(right.Content)]);
        var marks = left.Marks.Concat(right.Marks.Select(mark => mark.At(mark.Offset + length))).ToList();
        return new ParagraphState(left.Content.WithRuns(runs), marks);
    }

    public ParagraphState Map(ParagraphState state, int start, int end, Func<TextRun, TextRun> map)
    {
        Check(state, start);
        Check(state, end);
        if (end <= start) return state;
        (start, end) = WholeFields(Runs(state.Content), start, end);
        var (before, rest) = Cut(Runs(state.Content), start);
        var (middle, after) = Cut(rest, end - start);
        List<TextRun> runs = [.. before, .. middle.Select(map), .. after];
        return state with { Content = state.Content.WithRuns(Normalize(runs)) };
    }

    public ParagraphState AddMark(ParagraphState state, InlineMark mark)
    {
        Check(state, mark.Offset);
        return state with { Marks = [.. state.Marks, mark] };
    }

    public ParagraphState Restyle(ParagraphState state, int start, int end, IReadOnlyList<TextRun> runs)
    {
        Check(state, start);
        Check(state, end);
        if (end <= start || string.Concat(runs.Select(run => run.Text)) != state.Content.Text[start..end]) return state;
        var (before, rest) = Cut(Runs(state.Content), start);
        var (_, after) = Cut(rest, end - start);
        return state with { Content = state.Content.WithRuns(Normalize([.. before, .. runs, .. after])) };
    }

    public IReadOnlyList<TextRun> Slice(ParagraphState state, int start, int end)
    {
        Check(state, start);
        Check(state, end);
        if (end <= start) return [];
        var (_, rest) = Cut(Runs(state.Content), start);
        return Cut(rest, end - start).Before;
    }

    public IReadOnlyList<InlineMark> MarksRemovedBy(ParagraphState state, int start, int end)
    {
        if (end <= start) return [];
        (start, end) = Atomic(state, start, end);
        var controlEnds = new Dictionary<string, int>(StringComparer.Ordinal);
        var commentEnds = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var mark in state.Marks)
        {
            if (mark is ContentControlMark { Edge: MarkEdge.End } closing) controlEnds.TryAdd(closing.ControlId, closing.Offset);
            if (mark is CommentMark { Edge: MarkEdge.End } ending && (!commentEnds.TryGetValue(ending.CommentId, out var known) || ending.Offset < known))
                commentEnds[ending.CommentId] = ending.Offset;
        }
        var fields = CoveredFields(state, start, end);
        var controls = state.Marks.OfType<ContentControlMark>().Where(mark => mark.Edge == MarkEdge.Start && CoveredControl(controlEnds, mark, start, end))
            .Select(mark => mark.ControlId).ToHashSet(StringComparer.Ordinal);
        return [.. state.Marks.Where(mark => mark switch
        {
            NoteMark or ObjectMark => mark.Offset > start && mark.Offset < end,
            CommentMark { Edge: MarkEdge.Point or MarkEdge.Start } comment => mark.Offset > start && mark.Offset < end || CoveredComment(commentEnds, comment, start, end),
            ContentControlMark control => controls.Contains(control.ControlId),
            FieldCodeMark field => fields.Contains(field.FieldId),
            _ => false,
        })];
    }

    private HashSet<string> CoveredFields(ParagraphState state, int start, int end)
    {
        var begins = new Dictionary<string, int>(StringComparer.Ordinal);
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mark in state.Marks.OfType<FieldCodeMark>().OrderBy(mark => mark.Offset))
        {
            if (mark.Part == FieldCodePart.Begin) begins.TryAdd(mark.FieldId, mark.Offset);
            else if (mark.Part == FieldCodePart.End && begins.TryGetValue(mark.FieldId, out var opening)
                     && opening >= start && mark.Offset <= end && (start < opening || end > mark.Offset))
                covered.Add(mark.FieldId);
        }
        return covered;
    }

    private bool CoveredControl(IReadOnlyDictionary<string, int> ends, ContentControlMark control, int start, int end)
    {
        if (control.Offset < start || control.Offset > end) return false;
        if (!ends.TryGetValue(control.ControlId, out var closing)) return control.Offset > start && control.Offset < end;
        return closing <= end && (start < control.Offset || end > closing);
    }

    public int Snap(ParagraphState state, int offset)
    {
        var position = 0;
        foreach (var run in Runs(state.Content))
        {
            var finish = position + run.Text.Length;
            if (run.Field != TextFieldKind.None && offset > position && offset < finish) return finish;
            position = finish;
        }
        return offset;
    }

    public (int Start, int End) Atomic(ParagraphState state, int start, int end) =>
        end <= start ? (start, end) : WholeFields(Runs(state.Content), start, end);

    private bool CoveredComment(IReadOnlyDictionary<string, int> earliestEnds, CommentMark comment, int start, int end) =>
        comment.Edge == MarkEdge.Start && comment.Offset >= start && earliestEnds.TryGetValue(comment.CommentId, out var closing) && closing <= end;

    private (int Start, int End) WholeFields(IReadOnlyList<TextRun> runs, int start, int end)
    {
        var position = 0;
        foreach (var run in runs)
        {
            var finish = position + run.Text.Length;
            if (run.Field != TextFieldKind.None && position < end && finish > start)
            {
                start = Math.Min(start, position);
                end = Math.Max(end, finish);
            }
            position = finish;
        }
        return (start, end);
    }

    private bool StaysLeft(InlineMark mark, int offset, IReadOnlySet<string> collapsed) =>
        mark.Offset < offset || (mark.Offset == offset && (mark.StaysAtEnd || mark.FollowsPrecedingText || (mark.Span == MarkEdge.Start || mark.InsideRange) && collapsed.Contains(mark.Key)));

    private InlineMark ShiftForInsert(InlineMark mark, int offset, int length) =>
        mark.Offset > offset || (mark.Offset == offset && !mark.OpensRange && !mark.FollowsPrecedingText) ? mark.At(mark.Offset + length) : mark;

    private TextRun Template(IReadOnlyList<TextRun> runs, int offset)
    {
        if (runs.Count == 0) return new TextRun(string.Empty);
        var position = 0;
        TextRun? chosen = null;
        foreach (var run in runs)
        {
            if (offset > position && offset <= position + run.Text.Length)
            {
                chosen = run;
                break;
            }
            position += run.Text.Length;
        }
        var source = chosen ?? runs[0];
        return source with { Field = TextFieldKind.None, FieldArgument = null };
    }

    private (List<TextRun> Before, List<TextRun> After) Cut(IReadOnlyList<TextRun> runs, int offset)
    {
        var before = new List<TextRun>();
        var after = new List<TextRun>();
        var position = 0;
        foreach (var run in runs)
        {
            var end = position + run.Text.Length;
            if (end <= offset) before.Add(run);
            else if (position >= offset) after.Add(run);
            else
            {
                before.Add(run with { Text = run.Text[..(offset - position)] });
                after.Add(run with { Text = run.Text[(offset - position)..] });
            }
            position = end;
        }
        return (before, after);
    }

    private List<TextRun> Normalize(IEnumerable<TextRun> runs)
    {
        var result = new List<TextRun>();
        var pending = new System.Text.StringBuilder();
        TextRun? current = null;
        foreach (var run in runs.Where(run => run.Text.Length > 0))
        {
            if (current is not null && Mergeable(current, run))
            {
                pending.Append(run.Text);
                continue;
            }
            if (current is not null) result.Add(Joined(current, pending));
            current = run;
            pending.Clear().Append(run.Text);
        }
        if (current is not null) result.Add(Joined(current, pending));
        return result;
    }

    private TextRun Joined(TextRun run, System.Text.StringBuilder text) =>
        text.Length == run.Text.Length ? run : run with { Text = text.ToString() };

    private bool Mergeable(TextRun left, TextRun right) =>
        left.Field == TextFieldKind.None && right.Field == TextFieldKind.None
        && (left with { Text = string.Empty }) == (right with { Text = string.Empty });

    private void Check(ParagraphState state, int offset)
    {
        if (offset < 0 || offset > state.Length) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset outside the paragraph.");
    }
}

using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed record DocxParagraphSegment(ParagraphBlock Block, Deletion? DeletedMark);

public sealed class DocxParagraphMarkSplits(IParagraphText text)
{
    private sealed class Builder(string id, TextParagraph properties)
    {
        public string Id { get; } = id;

        public TextParagraph Properties { get; } = properties;

        public List<TextRun> Runs { get; } = [];

        public List<InlineMark> Marks { get; } = [];

        public int Length { get; private set; }

        public void Add(IReadOnlyList<TextRun> runs)
        {
            Runs.AddRange(runs);
            Length += runs.Sum(run => run.Text.Length);
        }

        public DocxParagraphSegment Close(Deletion? deletedMark) =>
            new(new ParagraphBlock(Id, Properties.WithRuns(Runs)) { Marks = Marks }, deletedMark);
    }

    public bool Splits(Deletion deletion) =>
        deletion.Removed.Count > 1 && deletion.Removed.Blocks.All(block => block is ParagraphBlock);

    public IReadOnlyList<DocxParagraphSegment>? Split(ParagraphBlock paragraph, DocxWriteContext context)
    {
        var joins = paragraph.Marks.Select((mark, index) => (Mark: mark, Index: index, Deletion: Joining(mark, context)))
            .Where(item => item.Deletion is not null)
            .Select(item => (item.Mark, item.Index, Deletion: item.Deletion!))
            .ToList();
        if (joins.Count == 0) return null;
        var state = paragraph.State;
        var marks = MarksBySegment(paragraph, joins, context);
        var segments = new List<DocxParagraphSegment>();
        var current = new Builder(paragraph.Id, paragraph.Content);
        var start = 0;
        var cut = 0;
        for (var join = 0; join < joins.Count; join++)
        {
            var (mark, _, deletion) = joins[join];
            current.Add(text.Slice(state, cut, mark.Offset));
            cut = mark.Offset;
            AddMarks(current, marks[join], start);
            var removed = deletion.Removed.Blocks.Cast<ParagraphBlock>().ToList();
            Removed(current, removed[0], deletion, context, segments.Count);
            segments.Add(current.Close(deletion));
            for (var middle = 1; middle < removed.Count - 1; middle++)
            {
                var whole = new Builder(Synthetic(paragraph, segments.Count), Bare(removed[middle].Content));
                Removed(whole, removed[middle], deletion, context, segments.Count);
                segments.Add(whole.Close(deletion));
            }
            current = new Builder(Synthetic(paragraph, segments.Count), Bare(removed[^1].Content));
            Removed(current, removed[^1], deletion, context, segments.Count);
            start = cut;
        }
        current.Add(text.Slice(state, cut, state.Length));
        AddMarks(current, marks[joins.Count], start);
        segments.Add(current.Close(null));
        return segments;
    }

    private Deletion? Joining(InlineMark mark, DocxWriteContext context) =>
        mark is ChangeMark { Edge: MarkEdge.Point } change && context.Document.Changes.Find(change.ChangeId) is Deletion deletion && Splits(deletion) ? deletion : null;

    private List<InlineMark>[] MarksBySegment(ParagraphBlock paragraph, IReadOnlyList<(InlineMark Mark, int Index, Deletion Deletion)> joins, DocxWriteContext context)
    {
        var segments = Enumerable.Range(0, joins.Count + 1).Select(_ => new List<InlineMark>()).ToArray();
        var keys = joins.Select(join => (join.Mark.Offset, join.Index)).Order().ToArray();
        var joining = joins.Select(join => join.Index).ToHashSet();
        for (var index = 0; index < paragraph.Marks.Count; index++)
        {
            if (joining.Contains(index)) continue;
            var mark = paragraph.Marks[index];
            var position = mark is ChangeMark change && context.Document.Changes.Find(change.ChangeId) is ParagraphFormatChange ? 0 : Before(keys, mark.Offset, index);
            segments[position].Add(mark);
        }
        return segments;
    }

    private int Before((int Offset, int Index)[] keys, int offset, int index)
    {
        var low = 0;
        var high = keys.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (keys[middle].Offset < offset || keys[middle].Offset == offset && keys[middle].Index < index) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    private void AddMarks(Builder builder, IReadOnlyList<InlineMark> marks, int start)
    {
        foreach (var mark in marks) builder.Marks.Add(mark.At(Math.Max(0, mark.Offset - start)));
    }

    private void Removed(Builder builder, ParagraphBlock removed, Deletion deletion, DocxWriteContext context, int segment)
    {
        if (removed.Length == 0 && removed.Marks.Count == 0 && !context.Document.Objects.AnchoredTo(removed.Id).Any()) return;
        var piece = new Deletion(deletion.Id + "\u0001" + segment.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\u0001" + removed.Id, deletion.Author, deletion.Date, new BlockContainer([removed]))
        {
            WithinInsertion = deletion.WithinInsertion,
        };
        context.SplitChanges[piece.Id] = piece;
        builder.Marks.Add(new ChangeMark(builder.Length, piece.Id, MarkEdge.Point));
    }

    private string Synthetic(ParagraphBlock paragraph, int segment) =>
        paragraph.Id + "\u0001" + segment.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private TextParagraph Bare(TextParagraph content) => content with { PageStyleName = null, PageNumberRestart = null, NextPageStyleName = null };
}

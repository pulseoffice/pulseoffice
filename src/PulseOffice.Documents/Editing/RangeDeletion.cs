using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Editing;

public enum AnnotationFate
{
    Purge,
    Retain,
}

public interface IRangeDeletion
{
    void Delete(TextDocument document, EditRecorder recorder, TrailPoint start, TrailPoint end, AnnotationFate fate = AnnotationFate.Purge);

    void RemoveText(TextDocument document, EditRecorder recorder, ParagraphBlock paragraph, int start, int end, AnnotationFate fate = AnnotationFate.Purge);

    void Join(TextDocument document, EditRecorder recorder, ParagraphBlock target, ParagraphBlock source);

    void Discard(TextDocument document, EditRecorder recorder, IEnumerable<Block> blocks);
}

public sealed class RangeDeletion(IParagraphText text, IBlockTree tree, IContentTrails trails, IObjectAnchoring anchoring, IStoryCatalog stories) : IRangeDeletion
{
    private const string ParagraphPrefix = "p";

    public void Delete(TextDocument document, EditRecorder recorder, TrailPoint start, TrailPoint end, AnnotationFate fate = AnnotationFate.Purge)
    {
        if (!ReferenceEquals(start.Trail.Story, end.Trail.Story))
        {
            throw new InvalidOperationException("A deletion cannot span different stories.");
        }
        if (start.CompareTo(end) > 0) throw new InvalidOperationException("The deletion range is not in document order.");
        var session = new DeletionSession(document, recorder, new MarkPurge(document, recorder, stories, fate));
        var common = trails.CommonDepth(start.Trail, end.Trail) - 1;
        var root = start.Trail.Steps[common].Container;
        DeleteIn(session, root, new TrailBound(start, common), new TrailBound(end, common));
        var caret = session.Caret(tree, start, end, root, start.Trail.Steps[common].Index);
        session.Purge.Flush(caret);
        session.Finish(caret);
    }

    public void RemoveText(TextDocument document, EditRecorder recorder, ParagraphBlock paragraph, int start, int end, AnnotationFate fate = AnnotationFate.Purge)
    {
        var purge = new MarkPurge(document, recorder, stories, fate);
        RemoveText(purge, paragraph, start, end);
        purge.Flush();
    }

    private void RemoveText(MarkPurge purge, ParagraphBlock paragraph, int start, int end)
    {
        purge.Recorder.Touch(paragraph);
        (start, end) = text.Atomic(paragraph.State, start, end);
        purge.Reach(paragraph, start, end);
        var removed = text.MarksRemovedBy(paragraph.State, start, end);
        paragraph.State = text.Delete(paragraph.State, start, end);
        foreach (var mark in removed) purge.Add(mark);
        if (end > start) purge.Recorder.Record(new TextDeleted(paragraph.Id, start, end - start));
    }

    public void Join(TextDocument document, EditRecorder recorder, ParagraphBlock target, ParagraphBlock source)
    {
        recorder.Touch(target).Touch(source);
        var join = target.Length;
        target.State = text.Concat(target.State, source.State);
        anchoring.Move(document, recorder, source.Id, target.Id);
        recorder.Record(new ParagraphsMerged(target.Id, source.Id, join));
    }

    public void Discard(TextDocument document, EditRecorder recorder, IEnumerable<Block> blocks)
    {
        var purge = new MarkPurge(document, recorder, stories, AnnotationFate.Purge);
        foreach (var block in blocks.ToList()) Discard(purge, block);
        purge.Flush();
    }

    private void Discard(MarkPurge purge, Block block)
    {
        foreach (var paragraph in tree.Paragraphs(new BlockContainer([block])))
        {
            purge.Discarded(paragraph.Id);
            foreach (var mark in paragraph.Marks) purge.Take(mark, paragraph.Id);
            purge.RemoveObjectsAnchoredTo(paragraph.Id);
        }
    }

    private void DeleteIn(DeletionSession session, BlockContainer container, TrailBound? from, TrailBound? to)
    {
        if (container.Count == 0) return;
        var first = from?.Index ?? 0;
        var last = to?.Index ?? container.Count - 1;
        session.Recorder.Touch(container);
        var removed = new List<Block>();
        if (first == last)
        {
            if (!DeleteInBlock(session, container[first], from?.Inner, to?.Inner)) removed.Add(container[first]);
        }
        else
        {
            var head = container[first];
            var tail = container[last];
            var keepHead = DeleteInBlock(session, head, from?.Inner, null);
            for (var index = first + 1; index < last; index++) removed.Add(container[index]);
            var keepTail = DeleteInBlock(session, tail, null, to?.Inner);
            if (!keepHead) removed.Add(head);
            if (!keepTail) removed.Add(tail);
            else if (keepHead && head is ParagraphBlock left && tail is ParagraphBlock right)
            {
                Join(session.Document, session.Recorder, left, right);
                removed.Add(right);
                session.Joined.Add(right.Id);
            }
        }
        foreach (var block in removed)
        {
            if (session.Joined.Contains(block.Id))
            {
                container.Remove(block);
                continue;
            }
            Discard(session.Purge, block);
            session.Removed.Add(new RemovedBlock(block, [.. tree.Paragraphs(new BlockContainer([block])).Select(paragraph => paragraph.Id)]));
            container.Remove(block);
        }
        if (container.Count == 0)
        {
            var filler = new ParagraphBlock(session.Document.NextId(ParagraphPrefix), string.Empty, session.FillerStyle ?? DefaultStyleNames.Standard);
            container.Add(filler);
            session.Fillers.Add(filler);
        }
    }

    private bool DeleteInBlock(DeletionSession session, Block block, TrailBound? from, TrailBound? to)
    {
        if (block is ParagraphBlock paragraph)
        {
            session.FillerStyle ??= paragraph.StyleName;
            RemoveText(session.Purge, paragraph, from?.Point.Offset ?? 0, to?.Point.Offset ?? paragraph.Length);
            return true;
        }
        var start = from is not null && IsAtStart(block, from) ? null : from;
        var finish = to is not null && IsAtEnd(block, to) ? null : to;
        if (start is null && finish is null) return false;
        if (block is SectionBlock section)
        {
            DeleteIn(session, section.Content, start, finish);
            return true;
        }
        if (block is TableBlock table)
        {
            var cells = table.Rows.SelectMany(row => row.Cells).Where(cell => !cell.Covered).ToList();
            var startCell = start is null ? 0 : cells.FindIndex(cell => ReferenceEquals(cell.Content, start.Container));
            var endCell = finish is null ? cells.Count - 1 : cells.FindIndex(cell => ReferenceEquals(cell.Content, finish.Container));
            for (var index = startCell; index <= endCell; index++)
            {
                var previous = session.FillerStyle;
                session.FillerStyle = cells[index].Content.Blocks.OfType<ParagraphBlock>().FirstOrDefault()?.StyleName;
                DeleteIn(session, cells[index].Content, index == startCell ? start : null, index == endCell ? finish : null);
                session.FillerStyle = previous;
            }
        }
        return true;
    }

    private bool IsAtStart(Block block, TrailBound bound)
    {
        var first = tree.Paragraphs(new BlockContainer([block])).FirstOrDefault();
        return first is not null && first.Id == bound.Point.Trail.Paragraph.Id && bound.Point.Offset == 0;
    }

    private bool IsAtEnd(Block block, TrailBound bound)
    {
        var last = tree.Paragraphs(new BlockContainer([block])).LastOrDefault();
        return last is not null && last.Id == bound.Point.Trail.Paragraph.Id && bound.Point.Offset == last.Length;
    }

    private sealed record RemovedBlock(Block Block, IReadOnlyList<string> ParagraphIds);

    private sealed class DeletionSession(TextDocument document, EditRecorder recorder, MarkPurge purge)
    {
        public TextDocument Document { get; } = document;

        public EditRecorder Recorder { get; } = recorder;

        public MarkPurge Purge { get; } = purge;

        public List<RemovedBlock> Removed { get; } = [];

        public HashSet<string> Joined { get; } = new(StringComparer.Ordinal);

        public List<ParagraphBlock> Fillers { get; } = [];

        public string? FillerStyle { get; set; }

        public void Finish(DocumentPosition caret)
        {
            foreach (var removed in Removed)
            {
                Recorder.Record(new BlockRemoved(removed.Block.Id, caret, removed.ParagraphIds));
            }
        }

        public DocumentPosition Caret(IBlockTree tree, TrailPoint start, TrailPoint end, BlockContainer root, int index)
        {
            var dead = Removed.SelectMany(removed => removed.ParagraphIds).ToHashSet(StringComparer.Ordinal);
            if (!dead.Contains(start.Trail.Paragraph.Id)) return new DocumentPosition(start.Trail.Paragraph.Id, start.Offset);
            if (!dead.Contains(end.Trail.Paragraph.Id) && !Joined.Contains(end.Trail.Paragraph.Id)) return new DocumentPosition(end.Trail.Paragraph.Id, 0);
            var survivor = tree.Paragraphs(new BlockContainer([root[Math.Min(index, root.Count - 1)]])).First();
            return new DocumentPosition(survivor.Id, 0);
        }
    }

    private sealed record LostMark(InlineMark Mark, string ParagraphId);

    private sealed class MarkPurge(TextDocument document, EditRecorder recorder, IStoryCatalog stories, AnnotationFate fate)
    {
        private readonly HashSet<string> _stripped = new(StringComparer.Ordinal);
        private readonly HashSet<string> _opened = new(StringComparer.Ordinal);
        private readonly HashSet<string> _closed = new(StringComparer.Ordinal);
        private readonly HashSet<string> _discarded = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LostMark> _lost = new(StringComparer.Ordinal);

        public EditRecorder Recorder { get; } = recorder;

        public void Add(InlineMark mark)
        {
            if (mark is NoteMark note && fate == AnnotationFate.Purge)
            {
                Recorder.Touch(document.Notes, () => document.Notes.All, document.Notes.Replace);
                document.Notes.Remove(note.NoteId);
            }
            if (mark is ObjectMark item && fate == AnnotationFate.Purge)
            {
                TouchObjects();
                document.Objects.Remove(item.ObjectId);
            }
            if (mark is CommentMark comment && !_stripped.Contains(comment.Key)) Strip(comment);
            if (mark is ContentControlMark or FieldCodeMark { Part: FieldCodePart.Begin or FieldCodePart.End }) _stripped.Add(mark.Key);
        }

        public void Discarded(string paragraphId) => _discarded.Add(paragraphId);

        public void Take(InlineMark mark, string paragraphId)
        {
            if (!(fate == AnnotationFate.Retain && Travels(mark))) Add(mark);
            Lose(mark, paragraphId);
        }

        public void Reach(ParagraphBlock paragraph, int start, int end)
        {
            foreach (var mark in paragraph.Marks.Where(Spans))
            {
                if (mark.Span == MarkEdge.Start && end == paragraph.Length && mark.Offset >= start) _opened.Add(mark.Key);
                if (mark.Span == MarkEdge.End && start == 0 && mark.Offset <= end) _closed.Add(mark.Key);
            }
        }

        public void RemoveObjectsAnchoredTo(string paragraphId)
        {
            if (fate == AnnotationFate.Retain) return;
            var anchored = document.Objects.AnchoredTo(paragraphId).Select(item => item.Id).ToList();
            if (anchored.Count == 0) return;
            TouchObjects();
            foreach (var id in anchored) document.Objects.Remove(id);
        }

        public void Flush(DocumentPosition? caret = null)
        {
            Cover();
            Settle(caret);
            _opened.Clear();
            _closed.Clear();
            _discarded.Clear();
            if (_stripped.Count == 0) return;
            foreach (var paragraph in stories.Paragraphs(document).Where(paragraph => paragraph.Marks.Any(mark => _stripped.Contains(mark.Key))).ToList())
            {
                Recorder.Touch(paragraph);
                paragraph.State = paragraph.State with { Marks = [.. paragraph.Marks.Where(mark => !_stripped.Contains(mark.Key))] };
            }
            _stripped.Clear();
        }

        private void Strip(CommentMark comment)
        {
            if (fate == AnnotationFate.Retain || document.Comments.Find(comment.CommentId) is null)
            {
                _stripped.Add(comment.Key);
                return;
            }
            Recorder.Touch(document.Comments, () => document.Comments.All, document.Comments.Replace);
            foreach (var gone in document.Comments.Thread(comment.CommentId).Select(reply => reply.Id).ToList())
            {
                _stripped.Add(new CommentMark(0, gone, MarkEdge.Point).Key);
                document.Comments.Remove(gone);
            }
        }

        private void Lose(InlineMark mark, string paragraphId)
        {
            if (Parks(mark)) _lost.TryAdd(mark.Key + "\u0000" + Role(mark), new LostMark(mark, paragraphId));
        }

        private bool Travels(InlineMark mark) => mark is CommentMark or ContentControlMark or FieldCodeMark;

        private bool Parks(InlineMark mark) =>
            mark is BookmarkMark or IndexEntryMark or ChangeMark && mark.Span is MarkEdge.Start or MarkEdge.End
            || fate == AnnotationFate.Retain && (mark is FieldCodeMark || Travels(mark) && mark.Span is MarkEdge.Start or MarkEdge.End);

        private bool Spans(InlineMark mark) => mark is FieldCodeMark || fate == AnnotationFate.Retain && mark is CommentMark or ContentControlMark;

        private string Role(InlineMark mark) => mark is FieldCodeMark field ? field.Part.ToString() : mark.Span.ToString();

        private void Cover()
        {
            var lost = _lost.Values.Where(item => Spans(item.Mark)).Select(item => item.Mark).ToList();
            var opened = _opened.Concat(lost.Where(mark => mark.Span == MarkEdge.Start).Select(mark => mark.Key)).ToHashSet(StringComparer.Ordinal);
            var closed = _closed.Concat(lost.Where(mark => mark.Span == MarkEdge.End).Select(mark => mark.Key)).ToHashSet(StringComparer.Ordinal);
            opened.IntersectWith(closed);
            if (opened.Count == 0) return;
            _stripped.UnionWith(opened);
            foreach (var key in _lost.Where(pair => opened.Contains(pair.Value.Mark.Key)).Select(pair => pair.Key).ToList()) _lost.Remove(key);
        }

        private void Settle(DocumentPosition? caret)
        {
            if (_lost.Count == 0) return;
            var lost = _lost.Values.ToList();
            _lost.Clear();
            var gone = lost.Select(item => item.Mark).ToHashSet<InlineMark>(ReferenceEqualityComparer.Instance);
            var surviving = stories.Paragraphs(document).SelectMany(paragraph => paragraph.Marks)
                .Where(mark => !gone.Contains(mark) && !_stripped.Contains(mark.Key)).Select(mark => (mark.Key, Role(mark))).ToHashSet();
            var keys = surviving.Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
            var orphaned = lost.Where(item => keys.Contains(item.Mark.Key) && !surviving.Contains((item.Mark.Key, Role(item.Mark)))).ToList();
            if (orphaned.Count == 0) return;
            if (caret is { } point)
            {
                Park(orphaned.Select(item => item.Mark), point);
                return;
            }
            foreach (var item in orphaned.Where(item => item.Mark is ChangeMark))
            {
                if (Nearest(item) is { } at) Park([item.Mark], at);
            }
            var widowed = orphaned.Where(item => item.Mark is not ChangeMark).Select(item => item.Mark.Key).ToHashSet(StringComparer.Ordinal);
            _stripped.UnionWith(widowed);
        }

        private DocumentPosition? Nearest(LostMark item)
        {
            var order = stories.Paragraphs(document).ToList();
            var index = order.FindIndex(paragraph => paragraph.Id == item.ParagraphId);
            if (index < 0) return null;
            var forward = item.Mark.Span == MarkEdge.Start;
            for (var step = forward ? index + 1 : index - 1; step >= 0 && step < order.Count; step += forward ? 1 : -1)
            {
                if (_discarded.Contains(order[step].Id)) continue;
                return new DocumentPosition(order[step].Id, forward ? 0 : order[step].Length);
            }
            return null;
        }

        private void Park(IEnumerable<InlineMark> marks, DocumentPosition point)
        {
            var host = stories.Find(document, point.ParagraphId).Paragraph;
            Recorder.Touch(host);
            host.State = host.State with { Marks = [.. host.Marks, .. marks.Select(mark => mark.At(point.Offset))] };
        }

        private void TouchObjects() => Recorder.Touch(document.Objects, () => document.Objects.All, document.Objects.Replace);
    }
}

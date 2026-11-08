using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;

namespace PulseOffice.Documents.Clipboard;

public interface IFragmentExtractor
{
    DocumentFragment Copy(TextDocument document, DocumentRange range);
}

public sealed class FragmentExtractor(IContentTrails trails, IParagraphText text, IBlockTree tree, IFragmentScanner scanner, IStyleClosure closure)
    : IFragmentExtractor
{
    public DocumentFragment Copy(TextDocument document, DocumentRange range)
    {
        var start = trails.Point(document, range.Start);
        var end = trails.Point(document, range.End);
        if (!ReferenceEquals(start.Trail.Story, end.Trail.Story)) throw new InvalidOperationException("A copy cannot span different stories.");
        if (start.CompareTo(end) > 0) throw new InvalidOperationException("The copy range is not in document order.");
        var common = trails.CommonDepth(start.Trail, end.Trail) - 1;
        var root = start.Trail.Steps[common].Container;
        var blocks = new BlockContainer(Extract(root, new TrailBound(start, common), new TrailBound(end, common)));
        Balance(blocks);
        var references = new FragmentReferences();
        scanner.Scan(blocks, references);
        var changes = Changes(document, references);
        var notes = Collect(references.Notes, document.Notes.Find, note => note.Clone(), note => note.Content, references);
        var comments = Comments(document, references);
        var objects = Objects(document, references);
        var styles = closure.Collect(document.Styles, references);
        return new DocumentFragment(blocks, styles, notes, comments, objects, changes);
    }

    private IEnumerable<Block> Extract(BlockContainer container, TrailBound? from, TrailBound? to)
    {
        var first = from?.Index ?? 0;
        var last = to?.Index ?? container.Count - 1;
        for (var index = first; index <= last; index++)
        {
            foreach (var block in ExtractBlock(container[index], index == first ? from?.Inner : null, index == last ? to?.Inner : null))
            {
                yield return block;
            }
        }
    }

    private IEnumerable<Block> ExtractBlock(Block block, TrailBound? from, TrailBound? to)
    {
        if (block is ParagraphBlock paragraph)
        {
            var state = paragraph.State;
            var stop = to?.Point.Offset ?? paragraph.Length;
            var begin = from?.Point.Offset ?? 0;
            var head = text.Split(state, stop).Left;
            var piece = text.Split(head, begin).Right;
            return [new ParagraphBlock(paragraph.Id, piece.Content) { Marks = piece.Marks }];
        }
        var start = from is not null && At(block, from, first: true) ? null : from;
        var finish = to is not null && At(block, to, first: false) ? null : to;
        if (block is SectionBlock section && (start is not null || finish is not null))
        {
            return [.. Extract(section.Content, start, finish)];
        }
        return [block.Clone()];
    }

    private bool At(Block block, TrailBound bound, bool first)
    {
        var paragraphs = tree.Paragraphs(new BlockContainer([block]));
        var edge = first ? paragraphs.FirstOrDefault() : paragraphs.LastOrDefault();
        return edge is not null && edge.Id == bound.Point.Trail.Paragraph.Id && bound.Point.Offset == (first ? 0 : edge.Length);
    }

    private void Balance(BlockContainer blocks)
    {
        var paragraphs = tree.Paragraphs(blocks).ToList();
        var opened = new HashSet<string>(StringComparer.Ordinal);
        var closed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mark in paragraphs.SelectMany(paragraph => paragraph.Marks))
        {
            if (mark.Span == MarkEdge.Start) opened.Add(mark.Key);
            if (mark.Span == MarkEdge.End) closed.Add(mark.Key);
        }
        foreach (var paragraph in paragraphs)
        {
            paragraph.Marks = [.. paragraph.Marks.Where(mark =>
                mark is ChangeMark || (mark.Span == MarkEdge.Point && !mark.InsideRange || (opened.Contains(mark.Key) && closed.Contains(mark.Key))))];
        }
        if (paragraphs.Count > 0) ClipChanges(paragraphs, opened, closed);
    }

    private void ClipChanges(List<ParagraphBlock> paragraphs, HashSet<string> opened, HashSet<string> closed)
    {
        var first = paragraphs[0];
        var last = paragraphs[^1];
        var changes = paragraphs.SelectMany(paragraph => paragraph.Marks).OfType<ChangeMark>().ToList();
        foreach (var id in changes.Where(mark => mark.Edge == MarkEdge.End && !opened.Contains(mark.Key)).Select(mark => mark.ChangeId).Distinct(StringComparer.Ordinal).ToList())
            first.State = text.AddMark(first.State, new ChangeMark(0, id, MarkEdge.Start));
        foreach (var id in changes.Where(mark => mark.Edge == MarkEdge.Start && !closed.Contains(mark.Key)).Select(mark => mark.ChangeId).Distinct(StringComparer.Ordinal).ToList())
            last.State = text.AddMark(last.State, new ChangeMark(last.Length, id, MarkEdge.End));
    }

    private List<TrackedChange> Changes(TextDocument document, FragmentReferences references)
    {
        return Collect(references.Changes, document.Changes.Find, change => change.Clone(), change => change is Deletion deletion ? deletion.Removed : new BlockContainer(), references);
    }

    private List<T> Collect<T>(List<string> ids, Func<string, T?> find, Func<T, T> clone, Func<T, BlockContainer> content, FragmentReferences references)
        where T : class
    {
        var result = new List<T>();
        for (var index = 0; index < ids.Count; index++)
        {
            if (find(ids[index]) is not { } item) continue;
            var copy = clone(item);
            result.Add(copy);
            scanner.Scan(content(copy), references);
        }
        return result;
    }

    private List<Comment> Comments(TextDocument document, FragmentReferences references)
    {
        foreach (var id in references.Comments.ToList())
        {
            foreach (var reply in document.Comments.Thread(id).Skip(1))
            {
                if (!references.Comments.Contains(reply.Id)) references.Comments.Add(reply.Id);
            }
        }
        return Collect(references.Comments, document.Comments.Find, comment => comment.Clone(), comment => comment.Content, references);
    }

    private List<AnchoredObject> Objects(TextDocument document, FragmentReferences references)
    {
        foreach (var paragraph in references.Paragraphs.ToList())
        {
            foreach (var item in document.Objects.AnchoredTo(paragraph))
            {
                if (!references.Objects.Contains(item.Id)) references.Objects.Add(item.Id);
            }
        }
        var result = new List<AnchoredObject>();
        for (var index = 0; index < references.Objects.Count; index++)
        {
            if (document.Objects.Find(references.Objects[index]) is not { } item) continue;
            var copy = item.Clone();
            result.Add(copy);
            if (copy.StyleName is not null) references.FrameStyles.Add(copy.StyleName);
            if (copy.Content.Story is { } story) scanner.Scan(story, references);
        }
        return result;
    }
}

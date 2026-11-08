using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;

using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Revisions;

public interface ITrackedEditing
{
    DocumentEdit InsertText(TextDocument document, UndoStack history, DocumentPosition at, string text, RevisionSignature signature);

    DocumentEdit Delete(TextDocument document, UndoStack history, DocumentRange range, RevisionSignature signature);

    DocumentEdit SplitParagraph(TextDocument document, UndoStack history, DocumentPosition at, RevisionSignature signature);

    DocumentEdit Paste(TextDocument document, UndoStack history, DocumentPosition at, DocumentFragment fragment, RevisionSignature signature);

    DocumentEdit MarkInserted(TextDocument document, UndoStack history, DocumentEdit edit, DocumentPosition start, DocumentPosition end, RevisionSignature signature);

    DocumentEdit Characters(TextDocument document, UndoStack history, DocumentRange range, RevisionSignature signature, Func<DocumentEdit> apply);

    DocumentEdit ReplaceText(TextDocument document, UndoStack history, DocumentRange range, string description, Func<string, string> transform, RevisionSignature signature);

    DocumentEdit Paragraphs(TextDocument document, UndoStack history, DocumentRange range, RevisionSignature signature, Func<DocumentEdit> apply);
}

public sealed class TrackedEditing(
    IDirectDocumentEditor inner,
    IChangeMarking marking,
    IChangeRegions regions,
    IFragmentExtractor extractor,
    IFragmentRemapper remapper,
    IStoryCatalog stories,
    IContentTrails trails,
    IBlockTree tree,
    IParagraphText text,
    IDeletedAnnotations annotations) : ITrackedEditing
{
    public DocumentEdit InsertText(TextDocument document, UndoStack history, DocumentPosition at, string value, RevisionSignature signature)
    {
        var paragraph = stories.Find(document, at.ParagraphId).Paragraph;
        at = at with { Offset = text.Snap(paragraph.State, at.Offset) };
        var foreign = paragraph.Marks.OfType<ChangeMark>()
            .Where(mark => mark.Edge == MarkEdge.End && mark.Offset == at.Offset && document.Changes.Find(mark.ChangeId) is Insertion insertion && insertion.Author != signature.Author)
            .Select(mark => mark.ChangeId).ToList();
        var lines = value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        using var transaction = history.BeginTransaction("insert-text");
        var edit = inner.InsertText(document, history, at, value);
        var location = stories.Find(document, at.ParagraphId).Location;
        var end = lines.Length == 1
            ? at with { Offset = at.Offset + lines[0].Length }
            : new DocumentPosition(location.Container[location.Index + lines.Length - 1].Id, lines[^1].Length);
        if (foreign.Count > 0)
        {
            var recorder = new EditRecorder(document, "insert-text");
            foreach (var id in foreign) marking.MoveEnd(document, recorder, id, at);
            recorder.Commit(history);
        }
        return MarkInserted(document, history, edit, at, end, signature);
    }

    public DocumentEdit SplitParagraph(TextDocument document, UndoStack history, DocumentPosition at, RevisionSignature signature)
    {
        using var transaction = history.BeginTransaction("split-paragraph");
        var edit = inner.SplitParagraph(document, history, at);
        if (edit.Changes.OfType<ParagraphSplit>().FirstOrDefault() is not { } split) return edit;
        return MarkInserted(document, history, edit, new DocumentPosition(split.ParagraphId, split.Offset), new DocumentPosition(split.NewParagraphId, 0), signature);
    }

    public DocumentEdit Paste(TextDocument document, UndoStack history, DocumentPosition at, DocumentFragment fragment, RevisionSignature signature)
    {
        if (fragment.IsEmpty) return inner.Paste(document, history, at, fragment);
        var tail = stories.Find(document, at.ParagraphId).Paragraph.Length - at.Offset;
        using var transaction = history.BeginTransaction("paste");
        var edit = inner.Paste(document, history, at, Settled(fragment));
        var last = edit.Changes.OfType<ParagraphSplit>().LastOrDefault(split => split.ParagraphId == at.ParagraphId)?.NewParagraphId ?? at.ParagraphId;
        var end = new DocumentPosition(last, stories.Find(document, last).Paragraph.Length - tail);
        return MarkInserted(document, history, edit, at, end, signature);
    }

    public DocumentEdit MarkInserted(TextDocument document, UndoStack history, DocumentEdit edit, DocumentPosition start, DocumentPosition end, RevisionSignature signature)
    {
        if (start == end) return edit;
        var map = regions.Map(document);
        if (map.Regions.Any(region => region.Change is Insertion && map.Covers(region, start, end))) return edit;
        var recorder = new EditRecorder(document, "track-insertion");
        if (map.Regions.FirstOrDefault(region => region.Change is Insertion && region.Change.Author == signature.Author && region.End == start) is { } adjacent)
            marking.MoveEnd(document, recorder, adjacent.Change.Id, end);
        else
            marking.AddInsertion(document, recorder, start, end, signature);
        recorder.Commit(history);
        return edit;
    }

    public DocumentEdit Delete(TextDocument document, UndoStack history, DocumentRange range, RevisionSignature signature)
    {
        if (range.IsCollapsed) return inner.Delete(document, history, range);
        using var transaction = history.BeginTransaction("delete");
        var pieces = Pieces(regions.Map(document), range);
        DocumentEdit? last = null;
        for (var index = pieces.Count - 1; index >= 0; index--) last = DeletePiece(document, history, pieces[index], signature);
        return last!;
    }

    private List<DocumentRange> Pieces(ChangeMap map, DocumentRange range)
    {
        if (!map.Knows(range.Start) || !map.Knows(range.End)) return [range];
        var cuts = map.Touching(range).Where(region => region.Change is Insertion).SelectMany(region => new[] { region.Start, region.End })
            .Where(edge => map.Knows(edge) && map.Compare(range.Start, edge) < 0 && map.Compare(edge, range.End) < 0)
            .Distinct().Order(Comparer<DocumentPosition>.Create(map.Compare)).ToList();
        var pieces = new List<DocumentRange>();
        var from = range.Start;
        foreach (var cut in cuts.Append(range.End))
        {
            pieces.Add(new DocumentRange(from, cut));
            from = cut;
        }
        return pieces;
    }

    private DocumentEdit DeletePiece(TextDocument document, UndoStack history, DocumentRange range, RevisionSignature signature)
    {
        var map = regions.Map(document);
        var covering = map.Regions.Where(region => region.Change is Insertion && map.Covers(region, range.Start, range.End)).Select(region => region.Change).ToList();
        if (covering.Any(change => change.Author == signature.Author))
        {
            var direct = inner.Delete(document, history, range);
            Prune(document, history);
            return direct;
        }
        var within = covering.FirstOrDefault()?.Id;
        var (removed, origins) = Removed(document, range);
        var after = OwnDeletionAt(document, map, range.End, signature, within);
        var before = OwnDeletionAt(document, map, range.Start, signature, within);
        var edit = inner.Delete(document, history, range, AnnotationFate.Retain);
        var recorder = new EditRecorder(document, "track-deletion");
        removed = annotations.Keep(document, recorder, removed, origins);
        if (after is not null && Joined(removed, after.Removed) is { } prefixed)
            marking.Replace(document, recorder, new Deletion(after.Id, after.Author, signature.Date, prefixed) { Comment = after.Comment, WithinInsertion = within });
        else if (before is not null && Joined(before.Removed, removed) is { } suffixed)
            marking.Replace(document, recorder, new Deletion(before.Id, before.Author, signature.Date, suffixed) { Comment = before.Comment, WithinInsertion = within });
        else
            marking.AddDeletion(document, recorder, range.Start, removed, signature, within);
        marking.Prune(document, recorder);
        recorder.Commit(history);
        return edit;
    }

    public DocumentEdit Characters(TextDocument document, UndoStack history, DocumentRange range, RevisionSignature signature, Func<DocumentEdit> apply)
    {
        var spans = Spans(document, range);
        var before = spans.Select(span => span.Paragraph.State).ToList();
        using var transaction = history.BeginTransaction("track-format");
        var edit = apply();
        var map = regions.Map(document);
        var recorder = new EditRecorder(document, "track-format");
        for (var i = 0; i < spans.Count; i++)
        {
            var (paragraph, from, to) = spans[i];
            if (to <= from || ReferenceEquals(paragraph.State.Content, before[i].Content)) continue;
            if (paragraph.Length != before[i].Length) continue;
            var start = new DocumentPosition(paragraph.Id, from);
            var end = new DocumentPosition(paragraph.Id, to);
            if (map.Regions.Any(region => region.Change is FormatChange or Insertion && map.Covers(region, start, end))) continue;
            marking.AddFormat(document, recorder, paragraph, from, to, text.Slice(before[i], from, to), signature);
        }
        recorder.Commit(history);
        return edit;
    }

    public DocumentEdit ReplaceText(TextDocument document, UndoStack history, DocumentRange range, string description, Func<string, string> transform, RevisionSignature signature)
    {
        using var transaction = history.BeginTransaction(description);
        var spans = Spans(document, range);
        DocumentEdit? last = null;
        for (var index = spans.Count - 1; index >= 0; index--)
        {
            var (paragraph, from, to) = spans[index];
            if (to <= from) continue;
            var runs = new List<TextRun>();
            text.Map(paragraph.State, from, to, run =>
            {
                runs.Add(run);
                return run;
            });
            var plain = string.Concat(runs.Select(run => run.Text));
            var changed = transform(plain);
            if (changed.Length != plain.Length || changed == plain) continue;
            var offset = plain.Length;
            for (var at = runs.Count - 1; at >= 0; at--)
            {
                var run = runs[at];
                offset -= run.Text.Length;
                if (run.Field != TextFieldKind.None) continue;
                var replacement = changed.Substring(offset, run.Text.Length);
                if (replacement == run.Text) continue;
                var prefix = 0;
                while (run.Text[prefix] == replacement[prefix]) prefix++;
                var suffix = 0;
                while (run.Text[^(suffix + 1)] == replacement[^(suffix + 1)]) suffix++;
                var start = from + offset + prefix;
                var end = from + offset + run.Text.Length - suffix;
                last = InsertText(document, history, new DocumentPosition(paragraph.Id, end), replacement[prefix..(run.Text.Length - suffix)], signature);
                last = Delete(document, history, new DocumentRange(new DocumentPosition(paragraph.Id, start), new DocumentPosition(paragraph.Id, end)), signature);
            }
        }
        return last ?? new EditRecorder(document, description).Commit(history);
    }

    public DocumentEdit Paragraphs(TextDocument document, UndoStack history, DocumentRange range, RevisionSignature signature, Func<DocumentEdit> apply)
    {
        var spans = Spans(document, range);
        var before = spans.Select(span => span.Paragraph.State).ToList();
        using var transaction = history.BeginTransaction("track-paragraph-format");
        var edit = apply();
        var recorder = new EditRecorder(document, "track-paragraph-format");
        for (var i = 0; i < spans.Count; i++)
        {
            var paragraph = spans[i].Paragraph;
            var previous = before[i].Content;
            var current = paragraph.State.Content;
            if (current with { Runs = previous.Runs, Text = previous.Text } == previous) continue;
            if (paragraph.Marks.OfType<ChangeMark>().Any(mark => document.Changes.Find(mark.ChangeId) is ParagraphFormatChange)) continue;
            marking.AddParagraphFormat(document, recorder, paragraph, previous, signature);
        }
        recorder.Commit(history);
        return edit;
    }

    private void Prune(TextDocument document, UndoStack history)
    {
        var recorder = new EditRecorder(document, "track-prune");
        marking.Prune(document, recorder);
        recorder.Commit(history);
    }

    private (BlockContainer Removed, IReadOnlyDictionary<string, string> Origins) Removed(TextDocument document, DocumentRange range)
    {
        var fragment = extractor.Copy(document, range);
        var bare = new DocumentFragment(new BlockContainer(fragment.Blocks.Blocks.Select(Bare)), fragment.Styles, [], [], []);
        var blocks = remapper.Remap(document, bare).Blocks;
        var origins = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (copy, source) in tree.Paragraphs(blocks).Zip(tree.Paragraphs(fragment.Blocks)))
        {
            copy.State = copy.State with { Marks = source.Marks };
            origins[source.Id] = copy.Id;
        }
        return (blocks, origins);
    }

    private DocumentFragment Settled(DocumentFragment fragment)
    {
        if (fragment.Changes.Count == 0) return fragment;
        var blocks = fragment.Blocks.Clone();
        foreach (var paragraph in tree.Paragraphs(blocks)) paragraph.Marks = [.. paragraph.Marks.Where(mark => mark is not ChangeMark)];
        return new DocumentFragment(blocks, fragment.Styles, fragment.Notes, fragment.Comments, fragment.Objects);
    }

    private Block Bare(Block block)
    {
        var copy = block.Clone();
        foreach (var paragraph in tree.Paragraphs(new BlockContainer([copy]))) paragraph.State = paragraph.State with { Marks = [] };
        return copy;
    }

    private Deletion? OwnDeletionAt(TextDocument document, ChangeMap map, DocumentPosition at, RevisionSignature signature, string? within) =>
        map.Regions.Where(region => region.IsPoint && region.Start == at).Select(region => region.Change).OfType<Deletion>()
            .FirstOrDefault(deletion => deletion.Author == signature.Author && deletion.WithinInsertion == within);

    private BlockContainer? Joined(BlockContainer first, BlockContainer second)
    {
        if (first.Blocks is not [ParagraphBlock left] || second.Blocks is not [ParagraphBlock right]) return null;
        var joined = text.Concat(left.State, right.State);
        return new BlockContainer([new ParagraphBlock(left.Id, joined.Content) { Marks = joined.Marks }]);
    }

    private List<(ParagraphBlock Paragraph, int From, int To)> Spans(TextDocument document, DocumentRange range)
    {
        var start = trails.Point(document, range.Start);
        var end = trails.Point(document, range.End);
        if (!ReferenceEquals(start.Trail.Story, end.Trail.Story) || start.CompareTo(end) > 0) return [];
        var first = start.Trail.Paragraph;
        var last = end.Trail.Paragraph;
        return [.. tree.Paragraphs(start.Trail.Story)
            .SkipWhile(paragraph => !ReferenceEquals(paragraph, first))
            .TakeWhile(paragraph => !ReferenceEquals(paragraph, last))
            .Append(last)
            .Select(paragraph => (paragraph, ReferenceEquals(paragraph, first) ? range.Start.Offset : 0, ReferenceEquals(paragraph, last) ? range.End.Offset : paragraph.Length))];
    }
}

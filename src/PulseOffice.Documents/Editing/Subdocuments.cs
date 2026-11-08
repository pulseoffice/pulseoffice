using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Revisions;

namespace PulseOffice.Documents.Editing;

public interface ISubdocuments
{
    DocumentFragment Whole(TextDocument source);

    DocumentEdit Insert(TextDocument document, UndoStack history, string afterParagraphId, TextDocument source, string link, string name);

    DocumentEdit Refresh(TextDocument document, UndoStack history, string sectionId, TextDocument source);

    IReadOnlyList<SectionBlock> Linked(TextDocument document);

    DocumentEdit Unlink(TextDocument document, UndoStack history, string sectionId);
}

public sealed class Subdocuments(
    IStoryCatalog stories,
    ISectionEditing sections,
    IFragmentAdoption adoption,
    IBlockTree tree,
    ISequenceFields sequences,
    ITrackedEditing tracked,
    IRevisionAuthor author)
    : ISubdocuments
{
    public DocumentFragment Whole(TextDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new DocumentFragment(
            source.Body.Clone(),
            source.Styles,
            [.. source.Notes.All],
            [.. source.Comments.All],
            [.. source.Objects.All.Where(item => item.Anchor is not PageAnchor).Select(item => item.Clone())],
            [.. source.Changes.All]);
    }

    public DocumentEdit Insert(TextDocument document, UndoStack history, string afterParagraphId, TextDocument source, string link, string name)
    {
        if (!document.Changes.IsRecording) return Place(document, history, afterParagraphId, source, link, name).Edit;
        using var transaction = history.BeginTransaction("subdocument-insert");
        var (edit, section) = Place(document, history, afterParagraphId, source, link, name);
        var anchor = stories.Find(document, afterParagraphId).Paragraph;
        var last = tree.Paragraphs(section.Content).Last();
        return tracked.MarkInserted(document, history, edit, new DocumentPosition(anchor.Id, anchor.Length), new DocumentPosition(last.Id, last.Length), author.Sign());
    }

    private (DocumentEdit Edit, SectionBlock Section) Place(TextDocument document, UndoStack history, string afterParagraphId, TextDocument source, string link, string name)
    {
        var found = stories.Find(document, afterParagraphId);
        var container = found.Location.Container;
        var recorder = new EditRecorder(document, "subdocument-insert");
        var copy = adoption.Adopt(document, recorder, Whole(source));
        recorder.Touch(container);
        var section = new SectionBlock(document.NextId("s"), name, new BlockContainer(Content(document, copy))) { LinkedFile = link };
        container.Insert(found.Location.Index + 1, section);
        sequences.Renumber(document, recorder);
        return (recorder.Commit(history), section);
    }

    public DocumentEdit Refresh(TextDocument document, UndoStack history, string sectionId, TextDocument source)
    {
        if (Linked(document).FirstOrDefault(section => section.Id == sectionId) is not { } target) return new EditRecorder(document, "subdocument-update").Commit(history);
        if (document.Changes.IsRecording) return Replace(document, history, target, source);
        var recorder = new EditRecorder(document, "subdocument-update");
        var stale = tree.Paragraphs(target.Content).Select(paragraph => paragraph.Id).ToHashSet(StringComparer.Ordinal);
        var orphans = document.Objects.All.Where(item => stale.Any(id => document.Objects.AnchoredTo(id).Contains(item))).ToList();
        if (orphans.Count > 0)
        {
            recorder.Touch(document.Objects, () => document.Objects.All, document.Objects.Replace);
            document.Objects.Replace(document.Objects.All.Except(orphans).ToList());
        }
        var copy = adoption.Adopt(document, recorder, Whole(source));
        recorder.Touch(target.Content);
        target.Content.Replace(Content(document, copy));
        sequences.Renumber(document, recorder);
        return recorder.Commit(history);
    }

    private DocumentEdit Replace(TextDocument document, UndoStack history, SectionBlock target, TextDocument source)
    {
        using var transaction = history.BeginTransaction("subdocument-update");
        var signature = author.Sign();
        var stale = tree.Paragraphs(target.Content).ToList();
        tracked.Delete(document, history, new DocumentRange(new DocumentPosition(stale[0].Id, 0), new DocumentPosition(stale[^1].Id, stale[^1].Length)), signature);
        var shell = tree.Paragraphs(target.Content).First();
        return tracked.Paste(document, history, new DocumentPosition(shell.Id, 0), Whole(source), signature);
    }

    public IReadOnlyList<SectionBlock> Linked(TextDocument document) =>
        [.. sections.Sections(document).Where(section => section.LinkedFile is { Length: > 0 })];

    public DocumentEdit Unlink(TextDocument document, UndoStack history, string sectionId)
    {
        var recorder = new EditRecorder(document, "subdocument-unlink");
        foreach (var story in stories.Stories(document))
        {
            if (tree.Locate(story, sectionId) is not { Block: SectionBlock section } location) continue;
            recorder.Touch(location.Container);
            var copy = new SectionBlock(section.Id, section.Name, section.Content)
            {
                Columns = section.Columns,
                Protected = section.Protected,
                ProtectionKey = section.ProtectionKey,
                Hidden = section.Hidden,
                Condition = section.Condition,
                Index = section.Index,
            };
            location.Container.RemoveAt(location.Index);
            location.Container.Insert(location.Index, copy);
            break;
        }
        return recorder.Commit(history);
    }

    private IReadOnlyList<Block> Content(TextDocument document, DocumentFragment copy) =>
        copy.Blocks.Count > 0 ? copy.Blocks.Blocks : [new ParagraphBlock(document.NextId("p"), string.Empty)];
}

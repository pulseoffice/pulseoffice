using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Clipboard;

public interface IStyleMerger
{
    void Merge(EditRecorder recorder, StyleSheet target, StyleSheet source);
}

public sealed class StyleMerger : IStyleMerger
{
    public void Merge(EditRecorder recorder, StyleSheet target, StyleSheet source)
    {
        Merge(recorder, target.Paragraph, source.Paragraph);
        Merge(recorder, target.Character, source.Character);
        Merge(recorder, target.List, source.List);
        Merge(recorder, target.Page, source.Page);
        Merge(recorder, target.Frame, source.Frame);
        Merge(recorder, target.Table, source.Table);
    }

    private void Merge<TStyle>(EditRecorder recorder, StyleCollection<TStyle> target, StyleCollection<TStyle> source) where TStyle : DocumentStyle
    {
        var missing = source.All.Where(style => !target.Contains(style.Name)).ToList();
        if (missing.Count == 0) return;
        recorder.Touch(target, () => target.All, target.ReplaceAll);
        foreach (var style in missing) target.Add((TStyle)style.Clone());
    }
}

public interface IFragmentAdoption
{
    DocumentFragment Adopt(TextDocument document, EditRecorder recorder, DocumentFragment fragment);
}

public sealed class FragmentAdoption(IFragmentRemapper remapper, IStyleMerger styles) : IFragmentAdoption
{
    public DocumentFragment Adopt(TextDocument document, EditRecorder recorder, DocumentFragment fragment)
    {
        var copy = remapper.Remap(document, fragment);
        styles.Merge(recorder, document.Styles, copy.Styles);
        if (copy.Notes.Count > 0)
        {
            recorder.Touch(document.Notes, () => document.Notes.All, document.Notes.Replace);
            foreach (var note in copy.Notes) document.Notes.Add(note);
        }
        if (copy.Comments.Count > 0)
        {
            recorder.Touch(document.Comments, () => document.Comments.All, document.Comments.Replace);
            foreach (var comment in copy.Comments) document.Comments.Add(comment);
        }
        if (copy.Objects.Count > 0)
        {
            recorder.Touch(document.Objects, () => document.Objects.All, document.Objects.Replace);
            foreach (var item in copy.Objects) document.Objects.Add(item);
        }
        if (copy.Changes.Count > 0)
        {
            recorder.Touch(document.Changes, () => document.Changes.All, document.Changes.Replace);
            foreach (var change in copy.Changes) document.Changes.Add(change);
        }
        return copy;
    }
}

public interface IFragmentPaster
{
    DocumentEdit Paste(TextDocument document, UndoStack history, DocumentPosition at, DocumentFragment fragment);
}

public sealed class FragmentPaster(IStoryCatalog stories, IParagraphText text, IFragmentAdoption adoption, IObjectAnchoring anchoring, Editing.ISequenceFields sequences)
    : IFragmentPaster
{
    private const string ParagraphPrefix = "p";

    public DocumentEdit Paste(TextDocument document, UndoStack history, DocumentPosition at, DocumentFragment fragment)
    {
        var found = stories.Find(document, at.ParagraphId);
        var recorder = new EditRecorder(document, "paste");
        if (fragment.IsEmpty) return recorder.Commit(history);
        var copy = adoption.Adopt(document, recorder, fragment);
        var paragraph = found.Paragraph;
        var container = found.Location.Container;
        recorder.Touch(paragraph).Touch(container);
        var (left, right) = text.Split(paragraph.State, at.Offset);
        var blocks = copy.Blocks.Blocks.ToList();
        if (paragraph.Length == 0 && blocks is [ParagraphBlock { } adopted, ..]) left = left with { Content = Placed(adopted.Content, left.Content) };
        if (blocks is [ParagraphBlock single])
        {
            paragraph.State = text.Concat(text.Concat(left, single.State), right);
            anchoring.Move(document, recorder, single.Id, paragraph.Id);
            if (single.Length > 0) recorder.Record(new TextInserted(paragraph.Id, at.Offset, single.Length));
            sequences.Renumber(document, recorder);
            return recorder.Commit(history);
        }
        var head = blocks[0] as ParagraphBlock;
        paragraph.State = head is null ? left : text.Concat(left, head.State);
        if (head is not null)
        {
            anchoring.Move(document, recorder, head.Id, paragraph.Id);
            blocks.RemoveAt(0);
        }
        ParagraphBlock tail;
        var merged = blocks.Count > 0 && blocks[^1] is ParagraphBlock;
        if (merged)
        {
            var last = (ParagraphBlock)blocks[^1];
            tail = new ParagraphBlock(last.Id, last.Content) { State = text.Concat(last.State, right) };
            blocks[^1] = tail;
        }
        else
        {
            tail = new ParagraphBlock(document.NextId(ParagraphPrefix), right.Content with { PageStyleName = null, PageNumberRestart = null }) { Marks = right.Marks };
            blocks.Add(tail);
        }
        anchoring.MoveMarked(document, recorder, right.Marks, paragraph.Id, tail.Id);
        container.InsertRange(found.Location.Index + 1, blocks);
        recorder.Record(new ParagraphSplit(paragraph.Id, at.Offset, tail.Id));
        if (head is not null && head.Length > 0) recorder.Record(new TextInserted(paragraph.Id, at.Offset, head.Length));
        if (merged && tail.Length - right.Length > 0) recorder.Record(new TextInserted(tail.Id, 0, tail.Length - right.Length));
        foreach (var block in blocks.Where(block => block != tail)) recorder.Record(new BlockRestored(block.Id));
        sequences.Renumber(document, recorder);
        return recorder.Commit(history);
    }

    private TextParagraph Placed(TextParagraph pasted, TextParagraph target) => pasted.WithRuns([]) with
    {
        PageBreakBefore = target.PageBreakBefore,
        ColumnBreakBefore = target.ColumnBreakBefore,
        PageStyleName = target.PageStyleName,
        PageNumberRestart = target.PageNumberRestart,
        NextPageStyleName = target.NextPageStyleName,
    };
}

public interface IDocumentClipboard
{
    DocumentFragment Copy(TextDocument document, DocumentRange range);

    DocumentFragment Cut(TextDocument document, UndoStack history, DocumentRange range);

    DocumentEdit Paste(TextDocument document, UndoStack history, DocumentPosition at, DocumentFragment fragment);
}

public sealed class DocumentClipboard(IFragmentExtractor extractor, IDocumentEditor editor) : IDocumentClipboard
{
    public DocumentFragment Copy(TextDocument document, DocumentRange range) => extractor.Copy(document, range);

    public DocumentFragment Cut(TextDocument document, UndoStack history, DocumentRange range)
    {
        var fragment = extractor.Copy(document, range);
        editor.Delete(document, history, range);
        return fragment;
    }

    public DocumentEdit Paste(TextDocument document, UndoStack history, DocumentPosition at, DocumentFragment fragment) =>
        editor.Paste(document, history, at, fragment);
}

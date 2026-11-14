using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing;

public enum ObjectOrder
{
    Front,
    Forward,
    Backward,
    Back,
}

public interface IObjectArrangement
{
    DocumentEdit Reanchor(TextDocument document, UndoStack history, string objectId, ObjectAnchor anchor, ObjectPlacement placement, DocumentPosition? markAt);

    DocumentEdit Order(TextDocument document, UndoStack history, string objectId, ObjectOrder order);

    bool Moves(TextDocument document, string objectId, ObjectOrder order);
}

public sealed class ObjectArrangement(IStoryCatalog stories, IParagraphText text) : IObjectArrangement
{
    public DocumentEdit Reanchor(TextDocument document, UndoStack history, string objectId, ObjectAnchor anchor, ObjectPlacement placement, DocumentPosition? markAt)
    {
        var item = document.Objects.Find(objectId) ?? throw new KeyNotFoundException($"Object '{objectId}' not found.");
        var recorder = new EditRecorder(document, "anchor-object").Touch(item);
        foreach (var paragraph in stories.Paragraphs(document).Where(paragraph => paragraph.Marks.Any(mark => mark is ObjectMark found && found.ObjectId == objectId)).ToList())
        {
            recorder.Touch(paragraph);
            paragraph.State = paragraph.State with { Marks = [.. paragraph.Marks.Where(mark => mark is not ObjectMark found || found.ObjectId != objectId)] };
        }
        item.Anchor = anchor;
        item.Placement = placement;
        if (anchor is CharacterAnchor or AsCharacterAnchor)
        {
            var at = markAt ?? throw new ArgumentException("A character anchor needs a position.", nameof(markAt));
            var target = stories.Find(document, at.ParagraphId).Paragraph;
            recorder.Touch(target);
            target.State = text.AddMark(target.State, new ObjectMark(text.Snap(target.State, at.Offset), objectId));
        }
        return recorder.Commit(history);
    }

    public DocumentEdit Order(TextDocument document, UndoStack history, string objectId, ObjectOrder order)
    {
        var recorder = new EditRecorder(document, "order-object");
        var sorted = Stacked(document);
        var position = sorted.FindIndex(item => item.Id == objectId);
        if (position < 0) throw new KeyNotFoundException($"Object '{objectId}' not found.");
        var moved = sorted[position];
        sorted.RemoveAt(position);
        var target = order switch
        {
            ObjectOrder.Front => sorted.Count,
            ObjectOrder.Back => 0,
            ObjectOrder.Forward => Math.Min(sorted.Count, position + 1),
            _ => Math.Max(0, position - 1),
        };
        sorted.Insert(target, moved);
        for (var index = 0; index < sorted.Count; index++)
        {
            if (sorted[index].ZOrder == index) continue;
            recorder.Touch(sorted[index]);
            sorted[index].ZOrder = index;
        }
        return recorder.Commit(history);
    }

    public bool Moves(TextDocument document, string objectId, ObjectOrder order)
    {
        var sorted = Stacked(document);
        var position = sorted.FindIndex(item => item.Id == objectId);
        if (position < 0) return false;
        return order is ObjectOrder.Front or ObjectOrder.Forward ? position < sorted.Count - 1 : position > 0;
    }

    private static List<AnchoredObject> Stacked(TextDocument document) =>
        document.Objects.All.Select((item, index) => (item, index)).OrderBy(pair => pair.item.ZOrder).ThenBy(pair => pair.index).Select(pair => pair.item).ToList();
}

public interface ISequenceFields
{
    bool Present(TextDocument document);

    void Renumber(TextDocument document, EditRecorder recorder);
}

public sealed class SequenceFields(Fields.IFieldUpdater fields) : ISequenceFields
{
    private readonly IReadOnlySet<TextFieldKind> _sequences = new HashSet<TextFieldKind> { TextFieldKind.Sequence };

    public bool Present(TextDocument document) => fields.Present(document, _sequences);

    public void Renumber(TextDocument document, EditRecorder recorder) =>
        fields.Refresh(document, recorder, new Fields.FieldUpdate(new Fields.NoFieldLayoutFacts(), DateTime.Now) { Kinds = _sequences });
}

public sealed record CaptionRequest(string Category, string Label, string Text, bool Above)
{
    public string Separator { get; init; } = ": ";
}

public interface ICaptions
{
    IReadOnlyList<string> Categories { get; }

    string? Category(TextDocument document, string objectId);

    DocumentEdit CaptionObject(TextDocument document, UndoStack history, string objectId, CaptionRequest request);

    DocumentEdit CaptionTable(TextDocument document, UndoStack history, string tableId, CaptionRequest request);
}

public sealed class Captions(IStoryCatalog stories, IBlockTree tree, IStyleEditing styles, ISequenceFields sequences) : ICaptions
{
    private const double CaptionAllowance = 24;

    public IReadOnlyList<string> Categories { get; } = ["Figure", "Illustration", "Table", "Text", "Drawing"];

    public string? Category(TextDocument document, string objectId) => document.Objects.Find(objectId)?.Content switch
    {
        TextBoxContent => "Text",
        EmbeddedContent { Value: PulseOffice.Drawing.Assets.RasterImageObject } => "Figure",
        EmbeddedContent { Value: PulseOffice.Document.Embedding.DataTableObject } => "Table",
        EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeObject or PulseOffice.Drawing.Shapes.ShapeGroupObject or PulseOffice.Drawing.Freeform.FreeformObject } => "Drawing",
        null => null,
        _ => "Illustration",
    };

    public DocumentEdit CaptionObject(TextDocument document, UndoStack history, string objectId, CaptionRequest request)
    {
        var item = document.Objects.Find(objectId) ?? throw new KeyNotFoundException($"Object '{objectId}' not found.");
        using var transaction = history.BeginTransaction("caption");
        EnsureStyle(document, history, request.Category);
        var recorder = new EditRecorder(document, "caption");
        var caption = Paragraph(document, request);
        if (item.Anchor is AsCharacterAnchor inline)
        {
            var location = stories.Find(document, inline.ParagraphId).Location;
            recorder.Touch(location.Container);
            location.Container.Insert(location.Index + (request.Above ? 0 : 1), caption);
        }
        else
        {
            Frame(document, recorder, item, caption, request);
        }
        recorder.Record(new BlockRestored(caption.Id));
        sequences.Renumber(document, recorder);
        return recorder.Commit(history);
    }

    public DocumentEdit CaptionTable(TextDocument document, UndoStack history, string tableId, CaptionRequest request)
    {
        var location = stories.Stories(document).Select(story => tree.Locate(story, tableId)).FirstOrDefault(found => found is { Block: TableBlock })
            ?? throw new KeyNotFoundException($"Table '{tableId}' not found.");
        using var transaction = history.BeginTransaction("caption");
        EnsureStyle(document, history, request.Category);
        var recorder = new EditRecorder(document, "caption").Touch(location.Container);
        var caption = Paragraph(document, request);
        location.Container.Insert(location.Index + (request.Above ? 0 : 1), caption);
        recorder.Record(new BlockRestored(caption.Id));
        sequences.Renumber(document, recorder);
        return recorder.Commit(history);
    }

    private void Frame(TextDocument document, EditRecorder recorder, AnchoredObject item, ParagraphBlock caption, CaptionRequest request)
    {
        recorder.Touch(document.Objects, () => document.Objects.All, document.Objects.Replace).Touch(item);
        var holder = new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Empty) { StyleName = request.Category, Runs = [] });
        holder.Marks = [new ObjectMark(0, item.Id)];
        var content = new BlockContainer(request.Above ? [caption, holder] : [holder, caption]);
        var frame = item.CopyAs(document.NextId("o"), item.Anchor, new TextBoxContent(content) { AutoGrowHeight = true });
        frame.Size = new DocumentSize(item.Size.Width, item.Size.Height + Twip.FromPoints(CaptionAllowance));
        frame.StyleName = null;
        frame.Name = null;
        frame.Contour = null;
        frame.WrapContour = false;
        frame.FlipHorizontal = false;
        frame.FlipVertical = false;
        frame.RotationDegrees = 0;
        foreach (var paragraph in stories.Paragraphs(document).Where(paragraph => paragraph.Marks.Any(mark => mark is ObjectMark found && found.ObjectId == item.Id)).ToList())
        {
            recorder.Touch(paragraph);
            paragraph.State = paragraph.State with { Marks = [.. paragraph.Marks.Select(mark => mark is ObjectMark found && found.ObjectId == item.Id ? new ObjectMark(found.Offset, frame.Id) : mark)] };
        }
        item.Anchor = new AsCharacterAnchor(holder.Id);
        item.Placement = new ObjectPlacement(Twip.Zero, Twip.Zero);
        item.Wrap = WrapMode.None;
        item.WrapSpacing = Margins.Zero;
        item.InBackground = false;
        item.WrapContour = false;
        item.WrapFirstParagraph = false;
        document.Objects.Add(frame);
    }

    private ParagraphBlock Paragraph(TextDocument document, CaptionRequest request)
    {
        var head = request.Label.Length == 0 ? string.Empty : request.Label + " ";
        var tail = request.Text.Length == 0 ? string.Empty : request.Separator + request.Text;
        var runs = new List<TextRun>();
        if (head.Length > 0) runs.Add(new TextRun(head));
        runs.Add(new TextRun("1") { Field = TextFieldKind.Sequence, FieldArgument = request.Category });
        if (tail.Length > 0) runs.Add(new TextRun(tail));
        return new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Empty) { StyleName = request.Category }.WithRuns(runs));
    }

    private void EnsureStyle(TextDocument document, UndoStack history, string category)
    {
        if (document.Styles.Paragraph.Contains(category)) return;
        styles.Add(document, history, document.Styles.Paragraph, new ParagraphStyle(category) { ParentName = DefaultStyleNames.Caption }, "caption-style");
    }
}

public interface IFrameLinking
{
    DocumentEdit Link(TextDocument document, UndoStack history, string fromId, string toId);

    DocumentEdit Unlink(TextDocument document, UndoStack history, string frameId);
}

public sealed class FrameLinking(IFrameChains chains) : IFrameLinking
{
    public DocumentEdit Link(TextDocument document, UndoStack history, string fromId, string toId)
    {
        var from = document.Objects.Find(fromId) ?? throw new KeyNotFoundException($"Object '{fromId}' not found.");
        var to = document.Objects.Find(toId) ?? throw new KeyNotFoundException($"Object '{toId}' not found.");
        if (!chains.CanLink(document, from, to)) throw new InvalidOperationException("These frames cannot be linked.");
        var recorder = new EditRecorder(document, "link-frames").Touch(document.Objects, () => document.Objects.All, document.Objects.Replace);
        var box = (TextBoxContent)from.Content;
        Replace(document, from, new TextBoxContent(box.Content) { AutoGrowHeight = false, NextFrameName = chains.Reference(to), Look = box.Look });
        return recorder.Commit(history);
    }

    public DocumentEdit Unlink(TextDocument document, UndoStack history, string frameId)
    {
        var frame = document.Objects.Find(frameId) ?? throw new KeyNotFoundException($"Object '{frameId}' not found.");
        var recorder = new EditRecorder(document, "unlink-frames").Touch(document.Objects, () => document.Objects.All, document.Objects.Replace);
        if (frame.Content is TextBoxContent { NextFrameName: not null } own)
            Replace(document, frame, new TextBoxContent(own.Content) { AutoGrowHeight = own.AutoGrowHeight, NextFrameName = null, Look = own.Look });
        else if (chains.Previous(document, frame) is { Content: TextBoxContent previous } before)
            Replace(document, before, new TextBoxContent(previous.Content) { AutoGrowHeight = previous.AutoGrowHeight, NextFrameName = null, Look = previous.Look });
        return recorder.Commit(history);
    }

    private void Replace(TextDocument document, AnchoredObject item, AnchoredContent content) =>
        document.Objects.Replace([.. document.Objects.All.Select(other => ReferenceEquals(other, item) ? item.CopyAs(item.Id, item.Anchor, content) : other)]);
}

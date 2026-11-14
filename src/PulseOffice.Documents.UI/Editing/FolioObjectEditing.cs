using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Controls.Objects;

namespace PulseOffice.Documents.UI.Editing;

public sealed record ObjectInsertion(EmbeddedObject Content, DocumentSize Size, ObjectAnchorKind Anchor, WrapMode Wrap);

public interface IFolioObjectEditing
{
    AnchoredObject? Selected(FolioEditor editor);

    bool Change(FolioEditor editor, Action<AnchoredObject> change);

    bool Replace(FolioEditor editor, Func<EmbeddedObject, EmbeddedObject> change);

    bool Wrap(FolioEditor editor, WrapMode mode);

    bool Contour(FolioEditor editor, bool on);

    bool Order(FolioEditor editor, ObjectOrder order);

    bool CanOrder(FolioEditor editor, ObjectOrder order);

    bool Rotate(FolioEditor editor, double degrees, bool relative);

    bool Flip(FolioEditor editor, bool horizontal);

    bool CanFlip(FolioEditor editor);

    bool Insert(FolioEditor editor, ObjectInsertion insertion);

    bool InsertTextFrame(FolioEditor editor, DocumentSize size);

    bool Caption(FolioEditor editor, CaptionRequest request);

    bool Link(FolioEditor editor, string targetId);

    bool Unlink(FolioEditor editor);

    IReadOnlyList<AnchoredObject> LinkTargets(FolioEditor editor);
}

public sealed class FolioObjectEditing(
    IObjectArrangement arrangement,
    ICaptions captions,
    IFrameLinking linking,
    IFrameChains chains,
    IContentFlipping flipping,
    IImageContourTracer tracer) : IFolioObjectEditing
{
    public AnchoredObject? Selected(FolioEditor editor) => editor.SelectedObjectId is { } id ? editor.Document.Objects.Find(id) : null;

    public bool Change(FolioEditor editor, Action<AnchoredObject> change)
    {
        if (editor.ReadOnly() || Selected(editor) is not { } item || editor.ObjectLocked(item)) return false;
        var probe = item.Clone();
        change(probe);
        if (probe.SameState(item)) return false;
        editor.Services.Editor.ChangeObject(editor.Document, editor.History, item.Id, change);
        return Settle(editor, item.Id);
    }

    public bool Replace(FolioEditor editor, Func<EmbeddedObject, EmbeddedObject> change)
    {
        if (editor.ReadOnly() || Selected(editor) is not { Content: EmbeddedContent embedded } item || editor.ObjectLocked(item)) return false;
        var next = change(embedded.Value);
        if (Equals(next, embedded.Value)) return false;
        Swap(editor, item, item.CopyAs(item.Id, item.Anchor, embedded.With(next)), "replace-object");
        return Settle(editor, item.Id);
    }

    public bool Wrap(FolioEditor editor, WrapMode mode) => Change(editor, item => item.Wrap = mode);

    public bool Contour(FolioEditor editor, bool on)
    {
        if (Selected(editor) is not { Content: EmbeddedContent embedded } item) return false;
        var traced = on && item.Contour is null && embedded.Value is RasterImageObject picture
            ? tracer.Trace(picture.Data.ToArray()) is { } points ? new ObjectContour([.. points.Select(point => new ContourPoint(Math.Round(point.X, 4), Math.Round(point.Y, 4)))]) : null
            : item.Contour;
        return Change(editor, target =>
        {
            target.WrapContour = on;
            target.Contour = on ? traced : target.Contour;
        });
    }

    public bool CanOrder(FolioEditor editor, ObjectOrder order) =>
        Selected(editor) is { } item && arrangement.Moves(editor.Document, item.Id, order);

    public bool Order(FolioEditor editor, ObjectOrder order)
    {
        if (editor.ReadOnly() || Selected(editor) is not { } item || editor.ObjectLocked(item)) return false;
        arrangement.Order(editor.Document, editor.History, item.Id, order);
        return Settle(editor, item.Id);
    }

    public bool Rotate(FolioEditor editor, double degrees, bool relative) =>
        double.IsFinite(degrees) && Change(editor, item => item.RotationDegrees = (((relative ? item.RotationDegrees + degrees : degrees) % 360) + 360) % 360);

    public bool CanFlip(FolioEditor editor) => Selected(editor) is { Content: EmbeddedContent { Value: ShapeObject or VectorGraphicObject or RasterImageObject } };

    public bool Flip(FolioEditor editor, bool horizontal)
    {
        if (!CanFlip(editor) || Selected(editor) is not { Content: EmbeddedContent embedded }) return false;
        if (flipping.Flips(embedded.Value)) return Replace(editor, content => flipping.Flip(content, horizontal));
        return Change(editor, item =>
        {
            if (horizontal) item.FlipHorizontal = !item.FlipHorizontal;
            else item.FlipVertical = !item.FlipVertical;
        });
    }

    public bool Insert(FolioEditor editor, ObjectInsertion insertion)
    {
        var id = editor.Document.NextId("o");
        var inserted = editor.Insert((documents, at) =>
        {
            ObjectAnchor anchor = insertion.Anchor switch
            {
                ObjectAnchorKind.AsCharacter => new AsCharacterAnchor(at.ParagraphId),
                ObjectAnchorKind.Character => new CharacterAnchor(at.ParagraphId),
                ObjectAnchorKind.Page => new PageAnchor(1),
                _ => new ParagraphAnchor(at.ParagraphId),
            };
            var item = new AnchoredObject(id, anchor, Content(editor, insertion.Content), insertion.Size)
            {
                Wrap = insertion.Wrap,
                ZOrder = editor.Document.Objects.All.Count == 0 ? 0 : editor.Document.Objects.All.Max(other => other.ZOrder) + 1,
            };
            documents.InsertObject(editor.Document, editor.History, item, at);
        }, "insert-object");
        return inserted && Settle(editor, id);
    }

    private EmbeddedContent Content(FolioEditor editor, EmbeddedObject value) => value is PulseOffice.Drawing.Shapes.ShapeObject { Shape: not PulseOffice.Drawing.Shapes.ShapeKind.Line } shape
        ? new EmbeddedContent(shape with { Text = null }, new BlockContainer([new ParagraphBlock(editor.Document.NextId("p"), ShapeParagraph(shape.Text ?? string.Empty))]))
        : new EmbeddedContent(value);

    private PulseOffice.Text.Rich.TextParagraph ShapeParagraph(string text) => new(text)
    {
        StyleName = DefaultStyleNames.FrameContents,
        Runs = text.Length == 0 ? [] : [new PulseOffice.Text.Rich.TextRun(text)],
        Align = PulseOffice.Text.Rich.TextAlign.Center,
        Explicit = PulseOffice.Text.Rich.ParagraphOverrides.Align,
    };

    public bool InsertTextFrame(FolioEditor editor, DocumentSize size)
    {
        var id = editor.Document.NextId("o");
        var inserted = editor.Insert((documents, at) =>
        {
            var content = new BlockContainer([new ParagraphBlock(editor.Document.NextId("p"), string.Empty, DefaultStyleNames.FrameContents)]);
            var item = new AnchoredObject(id, new CharacterAnchor(at.ParagraphId), new TextBoxContent(content), size)
            {
                Wrap = WrapMode.Parallel,
                StyleName = DefaultStyleNames.Frame,
                ZOrder = editor.Document.Objects.All.Count == 0 ? 0 : editor.Document.Objects.All.Max(other => other.ZOrder) + 1,
            };
            documents.InsertObject(editor.Document, editor.History, item, at);
        }, "insert-frame");
        return inserted && Settle(editor, id);
    }

    public bool Caption(FolioEditor editor, CaptionRequest request)
    {
        if (editor.ReadOnly()) return false;
        if (Selected(editor) is { } item)
        {
            if (editor.ObjectLocked(item)) return false;
            captions.CaptionObject(editor.Document, editor.History, item.Id, request);
            editor.Normalize();
            editor.SelectObject(null);
            editor.Refresh();
            return true;
        }
        if (editor.TableAt() is not { } table) return false;
        captions.CaptionTable(editor.Document, editor.History, table.Table.Id, request);
        editor.Normalize();
        editor.Refresh();
        return true;
    }

    public IReadOnlyList<AnchoredObject> LinkTargets(FolioEditor editor) =>
        Selected(editor) is { } item ? [.. editor.Document.Objects.All.Where(other => chains.CanLink(editor.Document, item, other))] : [];

    public bool Link(FolioEditor editor, string targetId)
    {
        if (editor.ReadOnly() || Selected(editor) is not { } item || editor.Document.Objects.Find(targetId) is not { } target || editor.ObjectLocked(item) || editor.ObjectLocked(target) || !chains.CanLink(editor.Document, item, target)) return false;
        linking.Link(editor.Document, editor.History, item.Id, targetId);
        return Settle(editor, item.Id);
    }

    public bool Unlink(FolioEditor editor)
    {
        if (editor.ReadOnly() || Selected(editor) is not { } item || editor.ObjectLocked(item) || !chains.Chained(editor.Document, item)) return false;
        linking.Unlink(editor.Document, editor.History, item.Id);
        return Settle(editor, item.Id);
    }

    private void Swap(FolioEditor editor, AnchoredObject item, AnchoredObject replacement, string description)
    {
        var recorder = new EditRecorder(editor.Document, description).Touch(editor.Document.Objects, () => editor.Document.Objects.All, editor.Document.Objects.Replace);
        editor.Document.Objects.Replace([.. editor.Document.Objects.All.Select(other => ReferenceEquals(other, item) ? replacement : other)]);
        recorder.Commit(editor.History);
    }

    private bool Settle(FolioEditor editor, string id)
    {
        editor.Normalize();
        editor.SelectObject(id);
        editor.Refresh();
        return true;
    }
}

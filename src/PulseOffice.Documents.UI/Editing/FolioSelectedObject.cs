using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Protection;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Documents.UI.Editing;

public sealed class FolioSelectedObject(FolioEditor editor)
{
    public bool EnterFrame(string objectId, DocumentPosition? at)
    {
        var document = editor.Document;
        var services = editor.Services;
        if (document.Objects.Find(objectId) is not { Content.Story: { } box }) return false;
        var frames = document.Objects.All.Select(item => item.Content.Story).OfType<BlockContainer>().ToList();
        if (at is { } hit && frames.Any(story => ReferenceEquals(story, services.Stories.Find(document, hit.ParagraphId).Story)))
        {
            editor.PlaceCaret(hit, false);
            return true;
        }
        if (box.Blocks.OfType<ParagraphBlock>().FirstOrDefault() is not { } first) return false;
        var last = services.Order.StoryOf(document, first.Id);
        editor.PlaceCaret(new DocumentPosition(last[^1].Id, last[^1].Length), false);
        return true;
    }

    public bool Delete()
    {
        if (editor.ReadOnly() || editor.SelectedObjectId is not { } id || editor.Document.Objects.Find(id) is { } item && Locked(item)) return false;
        editor.SelectObject(null);
        editor.Services.Editor.RemoveObject(editor.Document, editor.History, id);
        editor.Normalize();
        editor.Refresh();
        return true;
    }

    public bool Resize(DocumentSize size) =>
        Change(item => item.Size != size, item => item.Size = size);

    public bool Rotate(double degrees)
    {
        var normalized = ((degrees % 360) + 360) % 360;
        return Change(item => Math.Abs(item.RotationDegrees - normalized) > 0.01, item => item.RotationDegrees = normalized);
    }

    public bool Locked(AnchoredObject item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (editor.DocumentLocked()) return true;
        return item.Anchor.Accept(new AnchorParagraph()) is { } paragraph
            && editor.Services.Guard.Check(editor.Document, paragraph) != EditVerdict.Allowed;
    }

    private bool Change(Func<AnchoredObject, bool> differs, Action<AnchoredObject> change)
    {
        if (editor.ReadOnly() || editor.SelectedObjectId is not { } id || editor.Document.Objects.Find(id) is not { } item || Locked(item) || !differs(item)) return false;
        editor.Services.Editor.ChangeObject(editor.Document, editor.History, id, change);
        editor.Refresh();
        return true;
    }
}

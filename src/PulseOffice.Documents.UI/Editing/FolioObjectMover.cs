using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Documents.UI.Editing;

public sealed record ObjectDrop(int Page, double Left, double Top, double PointerX, double PointerY);

public enum ObjectAnchorKind
{
    Page,
    Paragraph,
    Character,
    AsCharacter,
}

public interface IFolioObjectMover
{
    bool Move(FolioEditor editor, string objectId, ObjectDrop drop);

    bool Reanchor(FolioEditor editor, string objectId, ObjectAnchorKind kind);

    bool Nudge(FolioEditor editor, string objectId, double dx, double dy);
}

public sealed class FolioObjectMover(IObjectArrangement arrangement, IFolioObjectHits hits) : IFolioObjectMover
{
    public bool Reanchor(FolioEditor editor, string objectId, ObjectAnchorKind kind)
    {
        if (editor.ReadOnly() || editor.Document.Objects.Find(objectId) is not { } item || Kind(item.Anchor) == kind || editor.ObjectLocked(item)) return false;
        var layout = editor.Layout;
        var queries = editor.Services.Queries;
        var box = hits.Find(layout, objectId, out var page);
        var at = box is null ? editor.Caret : queries.HitTest(layout, page, box.Bounds.X + 1, box.Bounds.Y + 1) ?? editor.Caret;
        var current = item.Anchor.Accept(new AnchorParagraph());
        var host = current is null ? editor.Document.Body : editor.Services.Stories.Find(editor.Document, current).Story;
        if (!ReferenceEquals(editor.Services.Stories.Find(editor.Document, at.ParagraphId).Story, host))
            at = current is null ? editor.Caret : new DocumentPosition(current, 0);
        if (kind != ObjectAnchorKind.Page && editor.RefusesEdit(at.ParagraphId)) return false;
        var left = box?.Bounds.X ?? 0;
        var top = box?.Bounds.Y ?? 0;
        var paragraphTop = box is null ? (double?)null : queries.Caret(layout, at with { Offset = 0 }) is { } start && start.Page == page ? start.Y : null;
        ObjectAnchor anchor = kind switch
        {
            ObjectAnchorKind.Page => new PageAnchor(Math.Max(1, page + 1)),
            ObjectAnchorKind.Character => new CharacterAnchor(at.ParagraphId),
            ObjectAnchorKind.AsCharacter => new AsCharacterAnchor(at.ParagraphId),
            _ => new ParagraphAnchor(at.ParagraphId),
        };
        var placement = kind switch
        {
            ObjectAnchorKind.Page => new ObjectPlacement(Points(left), Points(top)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page },
            ObjectAnchorKind.AsCharacter => new ObjectPlacement(Twip.Zero, Twip.Zero),
            _ => new ObjectPlacement(Points(left), Points(paragraphTop is { } y ? top - y : top))
            {
                Horizontal = HorizontalRelation.Page,
                Vertical = paragraphTop is null ? VerticalRelation.Page : VerticalRelation.Paragraph,
            },
        };
        arrangement.Reanchor(editor.Document, editor.History, objectId, anchor, placement, kind is ObjectAnchorKind.Character or ObjectAnchorKind.AsCharacter ? at : null);
        editor.Normalize();
        editor.SelectObject(objectId);
        editor.Refresh();
        return true;
    }

    public ObjectAnchorKind Kind(ObjectAnchor anchor) => anchor switch
    {
        PageAnchor => ObjectAnchorKind.Page,
        CharacterAnchor => ObjectAnchorKind.Character,
        AsCharacterAnchor => ObjectAnchorKind.AsCharacter,
        _ => ObjectAnchorKind.Paragraph,
    };

    public bool Move(FolioEditor editor, string objectId, ObjectDrop drop)
    {
        if (editor.ReadOnly() || editor.Document.Objects.Find(objectId) is not { } item || editor.ObjectLocked(item)) return false;
        var layout = editor.Layout;
        if (drop.Page < 0 || drop.Page >= layout.Pages.Count) return false;
        var queries = editor.Services.Queries;
        switch (item.Anchor)
        {
            case PageAnchor:
                Change(editor, objectId, target =>
                {
                    target.Anchor = new PageAnchor(drop.Page + 1);
                    target.Placement = new ObjectPlacement(Points(drop.Left), Points(drop.Top)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page };
                }, null);
                return true;
            case AsCharacterAnchor:
                if (queries.HitTest(layout, drop.Page, drop.PointerX, drop.PointerY) is not { } inline || editor.RefusesEdit(inline.ParagraphId)) return false;
                Change(editor, objectId, target => target.Anchor = new AsCharacterAnchor(inline.ParagraphId), inline);
                return true;
            default:
                if (queries.HitTest(layout, drop.Page, drop.PointerX, drop.PointerY) is not { } at || editor.RefusesEdit(at.ParagraphId)) return false;
                var top = queries.Caret(layout, at with { Offset = 0 }) is { } start && start.Page == drop.Page ? start.Y : (double?)null;
                var placement = new ObjectPlacement(Points(drop.Left), Points(top is { } y ? drop.Top - y : drop.Top))
                {
                    Horizontal = HorizontalRelation.Page,
                    Vertical = top is null ? VerticalRelation.Page : VerticalRelation.Paragraph,
                };
                var character = item.Anchor is CharacterAnchor;
                Change(editor, objectId, target =>
                {
                    target.Anchor = character ? new CharacterAnchor(at.ParagraphId) : new ParagraphAnchor(at.ParagraphId);
                    target.Placement = placement;
                }, character ? at : null);
                return true;
        }
    }

    public bool Nudge(FolioEditor editor, string objectId, double dx, double dy)
    {
        if (editor.ReadOnly() || editor.Document.Objects.Find(objectId) is not { Anchor: not AsCharacterAnchor } item || editor.ObjectLocked(item)) return false;
        Change(editor, objectId, target => target.Placement = target.Placement with
        {
            X = Points(target.Placement.X.ToPoints() + dx),
            Y = Points(target.Placement.Y.ToPoints() + dy),
        }, null);
        return true;
    }

    private void Change(FolioEditor editor, string objectId, Action<AnchoredObject> change, DocumentPosition? markAt)
    {
        editor.Services.Editor.ChangeObject(editor.Document, editor.History, objectId, change, markAt);
        editor.Normalize();
        editor.SelectObject(objectId);
        editor.Refresh();
    }

    private Twip Points(double value) => Twip.FromPoints(value);
}

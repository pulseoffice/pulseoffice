using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Links;
using ObjectBox = PulseOffice.Documents.Layout.Tree.ObjectBox;

namespace PulseOffice.Documents.UI.Editing;

public sealed record FolioObjectLinkTarget(string ObjectId, GroupMemberPath? Member);

public interface IFolioObjectLinks
{
    FolioObjectLinkTarget? Point(FolioEditor editor, ObjectBox box, double x, double y);

    FolioObjectLinkTarget? Target(FolioEditor editor);

    string? Link(FolioEditor editor, FolioObjectLinkTarget target);

    string? EffectiveLink(FolioEditor editor, FolioObjectLinkTarget target);

    bool Follow(FolioEditor editor, FolioObjectLinkTarget target);

    bool Set(FolioEditor editor, FolioObjectLinkTarget target, string? link);
}

public sealed class FolioObjectLinks(IGroupChildLinks children, IFolioObjectEditing objects, IFolioNavigation navigation, ILinkFollower follower) : IFolioObjectLinks
{
    private (FolioEditor Editor, FolioObjectLinkTarget Target)? _pointed;

    public FolioObjectLinkTarget? Point(FolioEditor editor, ObjectBox box, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(box);
        if (editor.Document.Objects.Find(box.ObjectId) is not { } item) return null;
        GroupMemberPath? member = null;
        if (item.Content is EmbeddedContent { Value: ShapeGroupObject group } && box.Bounds.Width > 0 && box.Bounds.Height > 0)
        {
            var (localX, localY) = Unrotated(x, y, box, item.RotationDegrees);
            var fractionX = (localX - box.Bounds.X) / box.Bounds.Width;
            var fractionY = (localY - box.Bounds.Y) / box.Bounds.Height;
            if (item.FlipHorizontal) fractionX = 1 - fractionX;
            if (item.FlipVertical) fractionY = 1 - fractionY;
            member = children.MemberAt(group, fractionX, fractionY);
        }
        var target = new FolioObjectLinkTarget(item.Id, member);
        _pointed = (editor, target);
        return target;
    }

    public FolioObjectLinkTarget? Target(FolioEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (editor.SelectedObjectIds.Count != 1 || editor.SelectedObjectId is not { } id || editor.Document.Objects.Find(id) is null) return null;
        return _pointed is { } pointed && ReferenceEquals(pointed.Editor, editor) && pointed.Target.ObjectId == id ? pointed.Target : new FolioObjectLinkTarget(id, null);
    }

    public string? Link(FolioEditor editor, FolioObjectLinkTarget target)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(target);
        if (editor.Document.Objects.Find(target.ObjectId) is not { } item) return null;
        return target.Member is { } member && item.Content is EmbeddedContent { Value: ShapeGroupObject group } ? children.Link(group, member) : item.Hyperlink;
    }

    public string? EffectiveLink(FolioEditor editor, FolioObjectLinkTarget target)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(target);
        if (editor.Document.Objects.Find(target.ObjectId) is not { } item) return null;
        var own = target.Member is { } member && item.Content is EmbeddedContent { Value: ShapeGroupObject group } ? children.EffectiveLink(group, member) : null;
        return own ?? (item.Hyperlink is { Length: > 0 } link ? link : null);
    }

    public bool Follow(FolioEditor editor, FolioObjectLinkTarget target) =>
        EffectiveLink(editor, target) is { } link && follower.Follow(link, place => navigation.Follow(editor, place));

    public bool Set(FolioEditor editor, FolioObjectLinkTarget target, string? link)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(target);
        if (editor.SelectedObjectId != target.ObjectId) return false;
        var value = link?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
        if (target.Member is { } member)
            return objects.Replace(editor, content => content is ShapeGroupObject group ? children.WithLink(group, member, value) : content);
        return objects.Change(editor, item => item.Hyperlink = value);
    }

    private (double X, double Y) Unrotated(double x, double y, ObjectBox box, double degrees)
    {
        if (degrees == 0 || !double.IsFinite(degrees)) return (x, y);
        var centerX = box.Bounds.X + box.Bounds.Width / 2;
        var centerY = box.Bounds.Y + box.Bounds.Height / 2;
        var radians = -degrees * Math.PI / 180;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        var (dx, dy) = (x - centerX, y - centerY);
        return (centerX + dx * cos - dy * sin, centerY + dx * sin + dy * cos);
    }
}

using PulseOffice.Document.Commands;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Documents.Objects;

public sealed record PlacedBox(string ObjectId, double X, double Y, double Width, double Height);

public interface IObjectGrouping
{
    bool CanGroup(TextDocument document, IReadOnlyList<string> objectIds);

    string? Group(TextDocument document, UndoStack history, IReadOnlyList<PlacedBox> boxes, int page);

    IReadOnlyList<string> Ungroup(TextDocument document, UndoStack history, string groupId, PlacedBox box, int page);
}

public sealed class ObjectGrouping(IPortableObjects portable, IGroupStories groupStories) : IObjectGrouping
{
    public bool CanGroup(TextDocument document, IReadOnlyList<string> objectIds) =>
        objectIds.Count > 1 && objectIds.All(id => document.Objects.Find(id) is { Content: EmbeddedContent, Anchor: not AsCharacterAnchor });

    public string? Group(TextDocument document, UndoStack history, IReadOnlyList<PlacedBox> boxes, int page)
    {
        if (boxes.Count < 2) return null;
        var items = boxes.Select(box => document.Objects.Find(box.ObjectId)).OfType<AnchoredObject>().ToList();
        if (items.Count != boxes.Count || items.Any(item => item.Content is not EmbeddedContent)) return null;
        var left = boxes.Min(box => box.X);
        var top = boxes.Min(box => box.Y);
        var width = Math.Max(1, boxes.Max(box => box.X + box.Width) - left);
        var height = Math.Max(1, boxes.Max(box => box.Y + box.Height) - top);
        var id = document.NextId("o");
        var children = boxes.Zip(items).Select(pair => new GroupChild(portable.Portable(pair.Second.Content)!,
            (pair.First.X - left) / width, (pair.First.Y - top) / height, pair.First.Width / width, pair.First.Height / height)
        { RotationDegrees = pair.Second.RotationDegrees }).ToList();
        var stories = new Dictionary<string, BlockContainer>(StringComparer.Ordinal);
        foreach (var content in items.Select(item => (EmbeddedContent)item.Content)) groupStories.Collect(content, stories);
        var group = new AnchoredObject(id, new PageAnchor(page + 1), new EmbeddedContent(new ShapeGroupObject { Id = id, Children = children }, null, stories),
            new DocumentSize(Twip.FromPoints(width), Twip.FromPoints(height)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(left), Twip.FromPoints(top)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page },
            Wrap = items[0].Wrap,
            ZOrder = items.Max(item => item.ZOrder),
        };
        var recorder = new EditRecorder(document, "group-objects");
        recorder.Touch(document.Objects, () => document.Objects.All, document.Objects.Replace);
        foreach (var item in items) document.Objects.Remove(item.Id);
        document.Objects.Add(group);
        recorder.Commit(history);
        return id;
    }

    public IReadOnlyList<string> Ungroup(TextDocument document, UndoStack history, string groupId, PlacedBox box, int page)
    {
        if (document.Objects.Find(groupId) is not { Content: EmbeddedContent { Value: ShapeGroupObject group } grouped } item) return [];
        var recorder = new EditRecorder(document, "ungroup-objects");
        recorder.Touch(document.Objects, () => document.Objects.All, document.Objects.Replace);
        document.Objects.Remove(groupId);
        var ids = new List<string>();
        var order = item.ZOrder;
        foreach (var child in group.Children)
        {
            var id = document.NextId("o");
            ids.Add(id);
            var (x, y, w, h) = child.Scaled(box.Width, box.Height);
            document.Objects.Add(new AnchoredObject(id, new PageAnchor(page + 1), groupStories.Released(child.Content, id, grouped.MemberStories),
                new DocumentSize(Twip.FromPoints(Math.Max(1, w)), Twip.FromPoints(Math.Max(1, h))))
            {
                Placement = new ObjectPlacement(Twip.FromPoints(box.X + x), Twip.FromPoints(box.Y + y)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page },
                Wrap = item.Wrap,
                RotationDegrees = child.RotationDegrees,
                ZOrder = order++,
            });
        }
        recorder.Commit(history);
        return ids;
    }
}

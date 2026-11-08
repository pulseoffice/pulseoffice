using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioObjectHits
{
    ObjectBox? At(PageBox page, double x, double y);

    ObjectBox? Find(DocumentLayout layout, string objectId, out int pageIndex);
}

public sealed class FolioObjectHits : IFolioObjectHits
{
    public ObjectBox? At(PageBox page, double x, double y) =>
        Objects(page)
            .Where(box => x >= box.Bounds.X && x <= box.Bounds.Right && y >= box.Bounds.Y && y <= box.Bounds.Bottom)
            .OrderBy(box => box.Layer == ObjectLayer.Behind ? 0 : box.Layer == ObjectLayer.Front ? 2 : 1)
            .LastOrDefault();

    public ObjectBox? Find(DocumentLayout layout, string objectId, out int pageIndex)
    {
        for (var index = 0; index < layout.Pages.Count; index++)
        {
            if (Objects(layout.Pages[index]).FirstOrDefault(box => box.ObjectId == objectId) is { } found)
            {
                pageIndex = index;
                return found;
            }
        }
        pageIndex = -1;
        return null;
    }

    private IEnumerable<ObjectBox> Objects(LayoutBox root)
    {
        foreach (var child in root.Children)
        {
            if (child is ObjectBox box) yield return box;
            foreach (var nested in Objects(child)) yield return nested;
        }
    }
}

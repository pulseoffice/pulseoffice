namespace PulseOffice.Documents.UI.Editing;

public interface IFolioObjectCycle
{
    bool Step(FolioEditor editor, int direction);
}

public sealed class FolioObjectCycle : IFolioObjectCycle
{
    public bool Step(FolioEditor editor, int direction)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var all = editor.Document.Objects.All;
        if (all.Count == 0) return false;
        var current = editor.SelectedObjectId is { } id ? IndexOf(all, id) : -1;
        var next = current < 0
            ? direction >= 0 ? 0 : all.Count - 1
            : ((current + Math.Sign(direction == 0 ? 1 : direction)) % all.Count + all.Count) % all.Count;
        editor.SelectObject(all[next].Id);
        return editor.SelectedObjectId == all[next].Id;
    }

    private int IndexOf(IReadOnlyList<PulseOffice.Documents.Objects.AnchoredObject> all, string id)
    {
        for (var index = 0; index < all.Count; index++)
            if (all[index].Id == id) return index;
        return -1;
    }
}

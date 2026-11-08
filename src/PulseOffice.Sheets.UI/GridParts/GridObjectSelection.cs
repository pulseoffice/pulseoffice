using PulseOffice.Drawing.Shapes;
namespace PulseOffice.Sheets.UI.GridParts;

internal sealed class GridObjectSelection(GridNotifications notifications, IGridSurface surface)
{
    private readonly List<PlacedObject> _selected = [];

    public PlacedObject? Last => _selected.Count > 0 ? _selected[^1] : null;

    public IReadOnlyList<PlacedObject> Items => _selected;

    public int Count => _selected.Count;

    public bool Contains(PlacedObject placed) =>
        _selected.Any(selected => ReferenceEquals(selected, placed));

    public PulseOffice.Drawing.Shapes.ShapeObject? Adjustable(PlacedObject placed) =>
        _selected.Count <= 1 && placed.Content is PulseOffice.Drawing.Shapes.ShapeObject shape ? shape : null;

    public void Select(PlacedObject? placed)
    {
        if (placed is null)
        {
            if (_selected.Count == 0) return;

            _selected.Clear();
            Announce();
            return;
        }

        if (_selected.Count == 1 && ReferenceEquals(_selected[0], placed)) return;

        _selected.Clear();
        _selected.Add(placed);
        Announce();
    }

    public void Toggle(PlacedObject placed)
    {
        ArgumentNullException.ThrowIfNull(placed);

        var existing = _selected.FindIndex(selected => ReferenceEquals(selected, placed));

        if (existing >= 0) _selected.RemoveAt(existing);
        else _selected.Add(placed);

        Announce();
    }

    public void SelectMany(IEnumerable<PlacedObject> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);

        _selected.Clear();
        _selected.AddRange(objects);
        Announce();
    }

    private void Announce()
    {
        notifications.SelectedObject(Last);
        surface.Invalidate();
    }
}

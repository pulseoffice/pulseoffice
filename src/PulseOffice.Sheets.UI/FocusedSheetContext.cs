using Avalonia.Controls;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Shell;

namespace PulseOffice.Sheets.UI;

public sealed class FocusedSheetContext : IActiveSheetContext
{
    private readonly IWindowScopedSlots<ActiveSheetContext> _slots;

    public FocusedSheetContext(IWindowScopedSlots<ActiveSheetContext> slots)
    {
        _slots = slots ?? throw new ArgumentNullException(nameof(slots));
        foreach (var slot in slots.All) slot.TargetChanged += OnSlotChanged;
        slots.SlotCreated += (_, slot) => slot.TargetChanged += OnSlotChanged;
        slots.SlotDropped += (_, slot) => slot.TargetChanged -= OnSlotChanged;
        slots.FocusChanged += (_, _) => TargetChanged?.Invoke(this, EventArgs.Empty);
    }

    private ActiveSheetContext Current => _slots.Current;

    public event EventHandler? TargetChanged;

    private void OnSlotChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, Current)) TargetChanged?.Invoke(this, e);
    }

    public Workbook? Workbook => Current.Workbook;

    public int SheetIndex => Current.SheetIndex;

    public CellRange Selection => Current.Selection;

    public UndoStack? History => Current.History;

    public Engine.Recalculation.CalculationEngine? Engine => Current.Engine;

    public double Zoom => Current.Zoom;

    public PlacedObject? SelectedObject => Current.SelectedObject;

    public IReadOnlyList<PlacedObject> SelectedObjects => Current.SelectedObjects;

    public bool PanesAreSplit => Current.PanesAreSplit;

    public ISheetTabSurface? Tabs => Current.Tabs;

    public ShapeText.ISheetShapeText? ShapeText => Current.ShapeText;

    public void Focus(SheetInputArea area) => Current.Focus(area);

    public void SelectRange(CellRange range) => Current.SelectRange(range);

    public bool ViewOption(string key) => Current.ViewOption(key);

    public void ToggleViewOption(string key) => Current.ToggleViewOption(key);

    public void SelectObject(PlacedObject? placed) => Current.SelectObject(placed);

    public void SelectObjects(IEnumerable<PlacedObject> objects) => Current.SelectObjects(objects);

    public void SelectSheet(int index) => Current.SelectSheet(index);

    public void Attach(SheetTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _slots.Own(Current, null);
        Current.Attach(target);
    }

    public void Detach(Workbook workbook) => Current.Detach(workbook);

    public void Attach(Control owner, SheetTarget target)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(target);
        var slot = _slots.SlotFor(owner);
        foreach (var other in _slots.All)
            if (!ReferenceEquals(other, slot) && ReferenceEquals(_slots.OwnerOf(other), owner)) Release(other);
        _slots.Own(slot, owner);
        slot.Attach(target);
    }

    public void Detach(Control owner, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(owner);
        foreach (var slot in _slots.All)
            if (ReferenceEquals(_slots.OwnerOf(slot), owner) && ReferenceEquals(slot.Workbook, workbook)) Release(slot);
    }

    private void Release(ActiveSheetContext slot)
    {
        _slots.Own(slot, null);
        if (slot.Workbook is { } workbook) slot.Detach(workbook);
    }

    public void RequestRefresh() => Current.RequestRefresh();

    public void FreezePanes(int rows, int columns) => Current.FreezePanes(rows, columns);

    public void SplitPanes(int rows, int columns) => Current.SplitPanes(rows, columns);

    public void ApplyRowFilter(Worksheet sheet, IReadOnlySet<int>? hiddenRows) => Current.ApplyRowFilter(sheet, hiddenRows);

    public void SetZoom(double zoom) => Current.SetZoom(zoom);

    public IDisposable? BeginReferencePick(ISheetReferencePickSink sink) => Current.BeginReferencePick(sink);
}

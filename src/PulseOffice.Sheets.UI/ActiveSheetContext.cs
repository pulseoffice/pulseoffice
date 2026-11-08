using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.UI;

public sealed record SheetTarget
{
    public required Workbook Workbook { get; init; }

    public required int SheetIndex { get; init; }

    public required UndoStack History { get; init; }

    public required Engine.Recalculation.CalculationEngine Engine { get; init; }

    public required Func<CellRange> Selection { get; init; }

    public required Action<CellRange> WriteSelection { get; init; }

    public required Action<string, bool> WriteViewOption { get; init; }

    public required Func<string, bool> ReadViewOption { get; init; }

    public required Action Refresh { get; init; }

    public required Action<int, int> FreezePanes { get; init; }

    public required Action<int, int> SplitPanes { get; init; }

    public required Func<bool> ReadPanesSplit { get; init; }

    public required Action<Worksheet, IReadOnlySet<int>?> ApplyRowFilter { get; init; }

    public required Func<double> ReadZoom { get; init; }

    public required Action<double> WriteZoom { get; init; }

    public required Func<PlacedObject?> ReadSelectedObject { get; init; }

    public required Func<IReadOnlyList<PlacedObject>> ReadSelectedObjects { get; init; }

    public required Action<IEnumerable<PlacedObject>> WriteSelectedObjects { get; init; }

    public required Action<int> SelectSheet { get; init; }

    public required Action<PlacedObject?> WriteSelectedObject { get; init; }

    public Func<ISheetReferencePickSink, IDisposable>? BeginReferencePick { get; init; }

    public ShapeText.ISheetShapeText? ShapeText { get; init; }

    public ISheetTabSurface? Tabs { get; init; }

    public Action<SheetInputArea>? FocusArea { get; init; }
}

public enum SheetInputArea { NameBox, FormulaBar, Grid }

public interface ISheetReferencePickSink
{
    void SelectionChanged();

    void Commit();

    void Cancel();
}

public interface IActiveSheetContext
{
    Workbook? Workbook { get; }
    int SheetIndex { get; }
    CellRange Selection { get; }

    void SelectRange(CellRange range);

    bool ViewOption(string key);

    void ToggleViewOption(string key);

    UndoStack? History { get; }

    Engine.Recalculation.CalculationEngine? Engine { get; }

    bool HasTarget => Workbook is not null;

    double Zoom { get; }

    PlacedObject? SelectedObject { get; }

    IReadOnlyList<PlacedObject> SelectedObjects { get; }

    void SelectObject(PlacedObject? placed);

    void SelectObjects(IEnumerable<PlacedObject> objects);

    void SelectSheet(int index);

    void Attach(SheetTarget target);

    void Detach(Workbook workbook);

    void RequestRefresh();

    void FreezePanes(int rows, int columns);

    void SplitPanes(int rows, int columns);

    bool PanesAreSplit { get; }

    void ApplyRowFilter(Worksheet sheet, IReadOnlySet<int>? hiddenRows);

    void SetZoom(double zoom);

    IDisposable? BeginReferencePick(ISheetReferencePickSink sink);

    event EventHandler? TargetChanged;

    ISheetTabSurface? Tabs => null;

    ShapeText.ISheetShapeText? ShapeText => null;

    void Focus(SheetInputArea area) { }
}

public sealed class ActiveSheetContext : IActiveSheetContext
{
    private SheetTarget? _target;

    public Workbook? Workbook => _target?.Workbook;

    public int SheetIndex => _target?.SheetIndex ?? 0;

    public UndoStack? History => _target?.History;

    public ISheetTabSurface? Tabs => _target?.Tabs;

    public ShapeText.ISheetShapeText? ShapeText => _target?.ShapeText;

    public void Focus(SheetInputArea area) => _target?.FocusArea?.Invoke(area);

    public Engine.Recalculation.CalculationEngine? Engine => _target?.Engine;

    public CellRange Selection => _target?.Selection() ?? CellRange.Single(CellAddress.First);

    public void SelectRange(CellRange range) => _target?.WriteSelection(range);

    public bool ViewOption(string key) => _target?.ReadViewOption(key) ?? false;

    public void ToggleViewOption(string key) => _target?.WriteViewOption(key, !ViewOption(key));

    public double Zoom => _target?.ReadZoom() ?? 1;

    public PlacedObject? SelectedObject => _target?.ReadSelectedObject();

    public IReadOnlyList<PlacedObject> SelectedObjects => _target?.ReadSelectedObjects() ?? [];

    public void SelectObject(PlacedObject? placed) => _target?.WriteSelectedObject(placed);

    public void SelectObjects(IEnumerable<PlacedObject> objects) => _target?.WriteSelectedObjects(objects);

    public void SelectSheet(int index) => _target?.SelectSheet(index);

    public event EventHandler? TargetChanged;

    public void Attach(SheetTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _target = target;
        TargetChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Detach(Workbook workbook)
    {
        if (!ReferenceEquals(Workbook, workbook)) return;

        _target = null;
        TargetChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RequestRefresh() => _target?.Refresh();

    public void FreezePanes(int rows, int columns) => _target?.FreezePanes(rows, columns);

    public void SplitPanes(int rows, int columns) => _target?.SplitPanes(rows, columns);

    public bool PanesAreSplit => _target?.ReadPanesSplit() ?? false;

    public void ApplyRowFilter(Worksheet sheet, IReadOnlySet<int>? hiddenRows) => _target?.ApplyRowFilter(sheet, hiddenRows);

    public void SetZoom(double zoom) => _target?.WriteZoom(zoom);

    public IDisposable? BeginReferencePick(ISheetReferencePickSink sink) => _target?.BeginReferencePick?.Invoke(sink);
}

public interface ISheetFormattingService
{
    bool CanFormat { get; }

    void ApplyToSelection(Func<CellStyle, CellStyle> change);

    void ApplyToSelection(string description, Func<CellStyle, CellStyle> change);

    void ApplyToSelection(string description, Func<CellRange, CellAddress, CellStyle, CellStyle> change);

    void ToggleOnSelection(Func<CellStyle, bool> read, Func<CellStyle, bool, CellStyle> write);

    CellStyle CurrentStyle { get; }

    bool Accepts(string commandId);
}

public sealed class SheetFormattingService(
    IActiveSheetContext context,
    ISelectionWorkArea workArea,
    IStyleBandFormatter bands,
    PulseOffice.Localization.ITextCatalog texts,
    ISheetEditGuard guard,
    PulseOffice.Shell.IDocumentWorkspace workspace,
    PulseOffice.UI.Controls.Services.IStatusBarService status) : ISheetFormattingService
{
    public bool CanFormat => context.Workbook is not null;

    public bool Accepts(string commandId) =>
        CanFormat && (context.ShapeText is not { IsActive: true } shape || shape.Supports(commandId));

    public CellStyle CurrentStyle
    {
        get
        {
            if (context.Workbook is not { } workbook) return CellStyle.Default;
            if (context.ShapeText is { IsActive: true } shape) return shape.Current;

            var sheet = workbook.Sheets[context.SheetIndex];
            return workbook.Styles.Get(sheet.StyleIdAt(context.Selection.Start));
        }
    }

    public void ApplyToSelection(Func<CellStyle, CellStyle> change) =>
        ApplyToSelection(texts["history.format"], change);

    public void ApplyToSelection(string description, Func<CellStyle, CellStyle> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        ApplyToSelection(description, (_, _, style) => change(style));
    }

    public void ApplyToSelection(string description, Func<CellRange, CellAddress, CellStyle, CellStyle> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[context.SheetIndex];
        if (context.ShapeText is { IsActive: true } shape)
        {
            var anchor = context.Selection;
            shape.Apply(description, style => change(anchor, anchor.Start, style));
            return;
        }
        if (Refused(sheet)) return;

        using var transaction = context.History?.BeginTransaction(description);

        var range = context.Selection;
        var work = Plan(sheet, range);
        var changed = new List<(CellAddress Address, int? Before, int After)>();

        foreach (var address in work.Cells)
        {
            var previous = sheet.OwnStyleAt(address);
            var next = workbook.Styles.Intern(change(range, address, workbook.Styles.Get(sheet.StyleIdAt(address))));

            if (previous == next) continue;

            sheet.SetStyle(address, next);
            changed.Add((address, previous, next));
        }

        if (changed.Count > 0)
            context.History?.Push(new DelegateAction(
                description,
                redo: () => { foreach (var entry in changed) sheet.SetStyle(entry.Address, entry.After); },
                undo: () => { foreach (var entry in changed) sheet.SetOwnStyle(entry.Address, entry.Before); }));

        if (work.SetsDefaults) ApplyDefaults(workbook, sheet, range, work, description, change);

        context.RequestRefresh();
    }

    private void ApplyDefaults(
        Workbook workbook, Worksheet sheet, CellRange range, SelectionWork work, string description,
        Func<CellRange, CellAddress, CellStyle, CellStyle> change)
    {
        var layout = sheet.Layout;
        var applied = bands.Apply(layout, range, work,
            (address, styleId) => workbook.Styles.Intern(change(range, address, workbook.Styles.Get(styleId))));
        if (!applied.Changed) return;

        context.History?.Push(new DelegateAction(description,
            redo: () => layout.Styles.Restore(applied.After),
            undo: () => layout.Styles.Restore(applied.Before)));
    }

    public void ToggleOnSelection(Func<CellStyle, bool> read, Func<CellStyle, bool, CellStyle> write)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);

        if (context.Workbook is not { } workbook) return;
        if (context.ShapeText is { IsActive: true } shape)
        {
            var value = !read(shape.Current);
            ApplyToSelection(style => write(style, value));
            return;
        }

        var sheet = workbook.Sheets[context.SheetIndex];
        var work = Plan(sheet, context.Selection);

        var styles = work.Cells.Select(sheet.StyleIdAt)
            .Concat(bands.DefaultStyles(sheet.Layout, context.Selection, work));

        var allHave = styles.All(id => read(workbook.Styles.Get(id)));

        ApplyToSelection(style => write(style, !allHave));
    }

    private bool Refused(Worksheet sheet)
    {
        if (workspace.Active?.IsReadOnly == true)
        {
            status.Report(texts["status.readOnly"], NotificationSeverity.Error, NotificationRemedies.EditMode);
            return true;
        }
        if (guard.CanFormat(sheet)) return false;

        status.Report(texts["status.protection.cellFormat"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }

    private SelectionWork Plan(Worksheet sheet, CellRange selection) =>
workArea.Plan(selection, sheet.UsedRange());
}

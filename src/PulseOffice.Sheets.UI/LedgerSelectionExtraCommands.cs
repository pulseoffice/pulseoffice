using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public interface ISheetSelectionSet
{
    IReadOnlySet<int> Sheets { get; }

    void Select(IEnumerable<int> sheets);

    void Clear();

    event EventHandler? Changed;
}

public sealed class SheetSelectionSet : ISheetSelectionSet
{
    private readonly HashSet<int> _sheets = [];

    public IReadOnlySet<int> Sheets => _sheets;

    public event EventHandler? Changed;

    public void Select(IEnumerable<int> sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);

        _sheets.Clear();
        foreach (var index in sheets) _sheets.Add(index);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (_sheets.Count == 0) return;

        _sheets.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class LedgerSelectionExtraCommands(
    IActiveSheetContext context,
    ISheetSelectionSet sheets,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.select.allSheets", _ =>
        {
            if (Context.Workbook is { } workbook)
            {
                sheets.Select(Enumerable.Range(0, workbook.Sheets.Count));
                status.Report(texts.Format("select.sheets.count", workbook.Sheets.Count));
            }

            return Task.CompletedTask;
        }),

        Command("ledger.select.sheets", ct => ChooseSheetsAsync(ct)),
        Command("ledger.select.nextSheet", _ => Extend(1)),
        Command("ledger.select.previousSheet", _ => Extend(-1)),

        Command("ledger.select.unprotectedCells", _ =>
        {
            Restrict(address => !Locked(address), "select.unprotected.count");
            return Task.CompletedTask;
        }),

        Command("ledger.select.visibleRows", _ =>
        {
            RestrictRows();
            return Task.CompletedTask;
        }),

        Command("ledger.select.visibleColumns", _ =>
        {
            RestrictColumns();
            return Task.CompletedTask;
        }),
    ];

    private CommandDefinition Command(string id, Func<CancellationToken, Task> run) => Prototype(id) with
    {
        MenuPath = "edit/select",
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        EnabledWhen = _ => Context.HasTarget,
        Execute = (_, ct) => run(ct),
    };

    private async Task ChooseSheetsAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var fields = workbook.Sheets
            .Select((sheet, index) => (FormField)new CheckField(
                $"sheet{index}", sheet.Name, sheets.Sheets.Contains(index) || index == Context.SheetIndex))
            .ToList();

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["select.sheets.explanation"], fields, texts),
            new DialogOptions { TitleKey = "command.ledger.select.sheets.title" }, ct).ConfigureAwait(true);

        if (values is null) return;

        var chosen = Enumerable
            .Range(0, workbook.Sheets.Count)
            .Where(index => values.TryGetValue($"sheet{index}", out var value) && value is true)
            .ToList();

        sheets.Select(chosen);
        status.Report(texts.Format("select.sheets.count", chosen.Count));
    }

    private Task Extend(int direction)
    {
        if (Context.Workbook is not { } workbook) return Task.CompletedTask;

        var current = sheets.Sheets.Count == 0 ? [Context.SheetIndex] : sheets.Sheets.ToHashSet();
        var next = Math.Clamp(Context.SheetIndex + direction, 0, workbook.Sheets.Count - 1);

        current.Add(next);
        sheets.Select(current);

        Context.SelectSheet(next);
        status.Report(texts.Format("select.sheets.count", current.Count));

        return Task.CompletedTask;
    }

    private bool Locked(CellAddress address)
    {
        if (Context.Workbook is not { } workbook) return false;

        var sheet = workbook.Sheets[Context.SheetIndex];

        return workbook.Styles.Get(sheet.StyleIdAt(address)).Locked;
    }

    private void Restrict(Func<CellAddress, bool> keep, string messageKey)
    {
        var sheet = Context.Workbook?.Sheets[Context.SheetIndex];
        var area = sheet is null ? null : BoundedToUsed(sheet, Context.Selection);
        var chosen = area is { } bounded ? bounded.Cells().Where(keep).ToList() : [];

        if (chosen.Count == 0)
        {
            status.Report(texts["select.nothing"], NotificationSeverity.Warning);
            return;
        }

        Context.SelectRange(new CellRange(
            new CellAddress(chosen.Min(cell => cell.Row), chosen.Min(cell => cell.Column)),
            new CellAddress(chosen.Max(cell => cell.Row), chosen.Max(cell => cell.Column))));

        status.Report(texts.Format(messageKey, chosen.Count));
    }

    private void RestrictRows()
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var range = Context.Selection;

        var visible = Enumerable
            .Range(range.Start.Row, range.End.Row - range.Start.Row + 1)
            .Where(row => !sheet.Layout.IsRowHidden(row))
            .ToList();

        if (visible.Count == 0)
        {
            status.Report(texts["select.nothing"], NotificationSeverity.Warning);
            return;
        }

        Context.SelectRange(new CellRange(
            new CellAddress(visible.Min(), range.Start.Column),
            new CellAddress(visible.Max(), range.End.Column)));

        status.Report(texts.Format("select.visibleRows.count", visible.Count));
    }

    private void RestrictColumns()
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var range = Context.Selection;

        var visible = Enumerable
            .Range(range.Start.Column, range.End.Column - range.Start.Column + 1)
            .Where(column => !sheet.Layout.IsColumnHidden(column))
            .ToList();

        if (visible.Count == 0)
        {
            status.Report(texts["select.nothing"], NotificationSeverity.Warning);
            return;
        }

        Context.SelectRange(new CellRange(
            new CellAddress(range.Start.Row, visible.Min()),
            new CellAddress(range.End.Row, visible.Max())));

        status.Report(texts.Format("select.visibleColumns.count", visible.Count));
    }
}

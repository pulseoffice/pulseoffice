using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Tables.Insert;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerTableInsertCommands(
    IActiveSheetContext context,
    ISheetRegions regions,
    ISheetSnapshots snapshots,
    ITableInsertPrompt prompt,
    ILedgerTableLooks looks,
    IStatusBarService status,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    private const int DefaultRows = 5;
    private const int DefaultColumns = 4;

    private TableInsertLimits Limits { get; } = new(1000, 100);

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.insert.table") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            DefaultGesture = "Ctrl+T",
            EnabledWhen = _ => Context.HasTarget && SheetUnprotected,
            RibbonControl = new TableGridSpec(Limits.MaxRows, Limits.MaxColumns),
            Execute = (command, ct) => InsertAsync(command.Argument as RibbonTableSize, ct),
        },
    ];

    private async Task InsertAsync(RibbonTableSize? quick, CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        var sheet = workbook.Sheets[Context.SheetIndex];
        var area = Area(sheet, Context.Selection);
        var choice = quick is { } size
            ? prompt.Quick(size.Rows, size.Columns, Limits)
            : await prompt.AskAsync(new TableInsertRequest("command.ledger.insert.table.title")
            {
                Rows = area?.RowCount ?? DefaultRows,
                Columns = area?.ColumnCount ?? DefaultColumns,
                Limits = Limits,
                ExplanationKey = "table.insert.ledger.explanation",
            }, ct).ConfigureAwait(true);
        if (choice is null || Context.Workbook != workbook || Context.Engine is not { } engine) return;

        var origin = area?.Start ?? Context.Selection.Start;
        var range = new CellRange(origin, new CellAddress(
            Math.Min(origin.Row + choice.Rows - 1, CellAddress.MaxRows - 1),
            Math.Min(origin.Column + choice.Columns - 1, CellAddress.MaxColumns - 1)));
        var sheetIndex = Context.SheetIndex;
        var title = texts["command.ledger.insert.table.title"];
        var name = FreeName(workbook);

        using (Context.History?.BeginTransaction(title))
        {
            Context.SelectRange(range);
            if (choice.HeaderRow) Headings(engine, sheet, sheetIndex, range, title);
            looks.ApplyToSelection(title, choice.Look, choice.HeaderRow, choice.BandedRows);
            var definition = new DatabaseRange { Name = name, SheetIndex = sheetIndex, Range = range, HasHeader = choice.HeaderRow };
            workbook.DatabaseRanges.Define(definition);
            Context.History?.Push(new DelegateAction(title,
                redo: () => workbook.DatabaseRanges.Define(definition),
                undo: () => workbook.DatabaseRanges.Remove(name)));
        }

        Context.RequestRefresh();
        status.Report(texts.Format("table.insert.ledger.done", name, range.ToString()), NotificationSeverity.Success);
    }

    private CellRange? Area(Worksheet sheet, CellRange selection)
    {
        if (!selection.IsSingleCell) return selection;
        var region = regions.CurrentRegion(sheet, selection.Start);
        return region.IsSingleCell ? null : region;
    }

    private void Headings(Engine.Recalculation.CalculationEngine engine, Worksheet sheet, int sheetIndex, CellRange range, string title)
    {
        var empty = Enumerable.Range(range.Start.Column, range.ColumnCount)
            .Select(column => new CellAddress(range.Start.Row, column))
            .Where(address => sheet[address] is { HasFormula: false } cell && cell.Value.IsEmpty)
            .ToList();
        if (empty.Count == 0) return;
        var before = snapshots.Capture(sheet);
        foreach (var address in empty)
            engine.SetValue(new SheetCell(sheetIndex, address), CellValue.Text(texts.Format("table.insert.ledger.column", address.Column - range.Start.Column + 1)));
        var after = snapshots.Capture(sheet);
        Context.History?.Push(new DelegateAction(title,
            redo: () => snapshots.Restore(engine, sheetIndex, after),
            undo: () => snapshots.Restore(engine, sheetIndex, before)));
    }

    private string FreeName(Workbook workbook)
    {
        var stem = texts["table.insert.ledger.name"];
        for (var index = 1; ; index++)
        {
            var candidate = $"{stem}{index}";
            if (workbook.DatabaseRanges.Find(candidate) is null) return candidate;
        }
    }
}

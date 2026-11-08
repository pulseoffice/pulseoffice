using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerFillCommands(
    IActiveSheetContext context,
    ISheetSnapshots snapshots,
    IDialogService dialogs,
    ITextCatalog texts,
    ISheetFill fill,
    ISheetFormattingService formatting,
    ISheetEditGuard guard,
    IStatusBarService status) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Fill("ledger.fill.down", FillDirection.Down, "Ctrl+D"),
        Fill("ledger.fill.right", FillDirection.Right, "Ctrl+R"),
        Fill("ledger.fill.up", FillDirection.Up, null),
        Fill("ledger.fill.left", FillDirection.Left, null),

        ClearFormatting(),

        Change("ledger.layout.hideRows", "format/rows", null, (sheet, _) =>
        {
            sheet.Layout.HideRows(Context.Selection.Start.Row, Context.Selection.End.Row);
        }),

        Change("ledger.layout.showRows", "format/rows", null, (sheet, _) =>
        {
            sheet.Layout.ShowRows(Context.Selection.Start.Row, Context.Selection.End.Row);
        }),

        Change("ledger.layout.hideColumns", "format/columns", null, (sheet, _) =>
        {
            sheet.Layout.HideColumns(Context.Selection.Start.Column, Context.Selection.End.Column);
        }),

        Change("ledger.layout.showColumns", "format/columns", null, (sheet, _) =>
        {
            sheet.Layout.ShowColumns(Context.Selection.Start.Column, Context.Selection.End.Column);
        }),
    ];

    public IEnumerable<CommandDefinition> GetDialogCommands() =>
    [
        Prompt("ledger.layout.rowHeight", "format/rows", ct => SizeAsync(ct, rows: true)),
        Prompt("ledger.layout.columnWidth", "format/columns", ct => SizeAsync(ct, rows: false)),
    ];

    private CommandDefinition Fill(string id, FillDirection direction, string? gesture) =>
        Change(id, "sheet/fill", gesture, (sheet, engine) =>
        {
            var selection = Context.Selection;
            var source = SourceOf(selection, direction);

            engine.Batch(() =>
            {
                foreach (var (address, cell) in fill.Extend(sheet, source, selection, direction))
                    if (cell.HasFormula)
                        engine.SetFormula(new SheetCell(Context.SheetIndex, address), cell.Formula!);
                    else
                        engine.SetValue(new SheetCell(Context.SheetIndex, address), cell.Value);
            });
        });

    private static CellRange SourceOf(CellRange selection, FillDirection direction) => direction switch
    {
        FillDirection.Down => new CellRange(
            selection.Start, new CellAddress(selection.Start.Row, selection.End.Column)),
        FillDirection.Up => new CellRange(
            new CellAddress(selection.End.Row, selection.Start.Column), selection.End),
        FillDirection.Right => new CellRange(
            selection.Start, new CellAddress(selection.End.Row, selection.Start.Column)),
        _ => new CellRange(
            new CellAddress(selection.Start.Row, selection.End.Column), selection.End),
    };

    private CommandDefinition Change(
        string id, string menuPath, string? gesture,
        Action<Worksheet, Engine.Recalculation.CalculationEngine> change) =>
        Prototype(id) with
        {
            IconKey = id,
            MenuPath = menuPath,
            ContextPaths = menuPath.EndsWith("rows", StringComparison.Ordinal)
                ? ["row/structure"]
                : menuPath.EndsWith("columns", StringComparison.Ordinal)
                    ? ["column/structure"]
                    : id.StartsWith("ledger.fill.", StringComparison.Ordinal)
                        ? ["cell/cells/fill"]
                        : ["cell/format"],
            DefaultGesture = gesture,
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => Context.HasTarget && (IsFill(id) || SheetUnprotected),
            Execute = (_, _) =>
            {
                Apply(id, change);
                return Task.CompletedTask;
            },
        };

    private CommandDefinition ClearFormatting() =>
        Change("ledger.format.clear", "format/cells", null, (_, _) => { }) with
        {
            Execute = (_, _) =>
            {
                formatting.ApplyToSelection(texts["command.ledger.format.clear.title"], _ => CellStyle.Default);
                return Task.CompletedTask;
            },
        };

    private CommandDefinition Prompt(string id, string menuPath, Func<CancellationToken, Task> run) =>
        Prototype(id) with
        {
            MenuPath = menuPath,
            ContextPaths = menuPath.EndsWith("rows", StringComparison.Ordinal)
                ? ["row/structure"]
                : ["column/structure"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => Context.HasTarget && SheetUnprotected,
            Execute = (_, ct) => run(ct),
        };

    private static bool IsFill(string id) => id.StartsWith("ledger.fill.", StringComparison.Ordinal);

    private void Apply(string id, Action<Worksheet, Engine.Recalculation.CalculationEngine> change)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var sheetIndex = Context.SheetIndex;
        var sheet = workbook.Sheets[sheetIndex];

        if (IsFill(id) ? !guard.CanWrite(workbook, sheet, Context.Selection) : !guard.CanFormat(sheet))
        {
            status.Report(texts[IsFill(id) ? "status.protection.locked" : "status.protection.format"],
                NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        var before = snapshots.Capture(sheet);
        change(sheet, engine);
        var after = snapshots.Capture(sheet);

        Context.History?.Push(new DelegateAction(
            texts[$"command.{id}.title"],
            redo: () => snapshots.Restore(engine, sheetIndex, after),
            undo: () => snapshots.Restore(engine, sheetIndex, before)));

        Context.RequestRefresh();
    }

    private async Task SizeAsync(CancellationToken ct, bool rows)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var selection = Context.Selection;

        var current = rows
            ? sheet.Layout.RowHeight(selection.Start.Row)
            : sheet.Layout.ColumnWidth(selection.Start.Column);

        var form = new FormPromptView(
            texts[rows ? "dialog.rowHeight.explanation" : "dialog.columnWidth.explanation"],
            [
                new NumberField("size",
                    texts[rows ? "dialog.rowHeight.value" : "dialog.columnWidth.value"],
                    Math.Round(current.ToCentimeters(), 2), 0.1, 50, 0.1),
            ],
            texts);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            form,
            new DialogOptions { TitleKey = rows ? "dialog.rowHeight.heading" : "dialog.columnWidth.heading" },
            ct).ConfigureAwait(true);

        if (values is null) return;

        var size = Twip.FromCentimeters(
            Convert.ToDouble(values["size"], System.Globalization.CultureInfo.InvariantCulture));

        Apply(rows ? "ledger.layout.rowHeight" : "ledger.layout.columnWidth", (target, _) =>
        {
            if (rows) target.Layout.SetRowHeights(selection.Start.Row, selection.End.Row, size);
            else target.Layout.SetColumnWidths(selection.Start.Column, selection.End.Column, size);
        });
    }
}

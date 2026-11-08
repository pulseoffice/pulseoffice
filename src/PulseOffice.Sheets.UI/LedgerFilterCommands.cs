using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerFilterCommands(
    IActiveSheetContext context,
    ISheetRangeFilters rangeFilters,
    ISheetRegions regions,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    DataDialogs.LedgerDialogKit kit,
    DataDialogs.IFilterColumnFacts facts,
    DataDialogs.IFilterCriteriaCodec codec,
    ISheetSort sorter,
    ISheetSortGate sortGate) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.data.autoFilter") with
        {
            IconKey = "ledger.data.autoFilter",
            MenuPath = "data/filter",
            ContextPaths = ["cell/data"],
            DefaultGesture = "Ctrl+Shift+L",
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                Toggle();
                return Task.CompletedTask;
            },
        },

        Prototype("ledger.data.filterClear") with
        {
            MenuPath = "data",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            RequiredSelection = SelectionKind.CellRange,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Workbook is { } workbook
                && workbook.Sheets[Context.SheetIndex].AutoFilter.Filters.Count > 0,
            Execute = (_, _) =>
            {
                ClearAll();
                return Task.CompletedTask;
            },
        },
    ];

    private void ClearAll()
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        if (Refused(sheet) || sheet.AutoFilter.Filters.Count == 0) return;

        var before = sheet.AutoFilter.Save();
        sheet.AutoFilter.Clear();
        Record(sheet, before);
        rangeFilters.Refresh(Context, sheet);
        status.Report(texts["filter.cleared"]);
    }

    private bool Refused(Worksheet sheet)
    {
        if (sheet.Protection is not { IsEnabled: true, AllowFilter: false }) return false;

        status.Report(texts["status.protection.filter"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }

    public async Task ShowMenuAsync(int columnOffset, CancellationToken ct = default)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        if (sheet.AutoFilter.Range is not { } range) return;

        var header = sheet[new CellAddress(range.Start.Row, range.Start.Column + columnOffset)]
            .Value.ToString();

        var column = facts.Read(sheet, range, columnOffset, sheet.AutoFilter.HasHeader, workbook.Styles, workbook.NumberFormats);

        var choice = await dialogs.ShowAsync<FilterChoice>(
            new FilterMenuView(
                header.Length > 0 ? header : texts["filter.column"],
                column,
                sheet.AutoFilter.For(columnOffset),
                kit,
                codec),
            new DialogOptions { TitleKey = "filter.heading", PreferredWidth = 380 },
            ct).ConfigureAwait(true);

        if (choice is null || Context.History is null) return;
        if (Refused(sheet)) return;

        if (choice.SortDescending is { } descending)
        {
            if (sortGate.Refuses(workbook, sheet, range)) return;

            sorter.Sort(sheet, range, [new SortKey(columnOffset, !descending)], sheet.AutoFilter.HasHeader, Context.History, texts["history.ledger.sort"]);
            if (sheet.AutoFilter.IsActive) ApplyFilters(sheet);
            Context.RequestRefresh();
            return;
        }

        var before = sheet.AutoFilter.Save();
        sheet.AutoFilter.Set(new ColumnFilter(columnOffset)
        {
            AllowedValues = choice.AllowedValues,
            Criteria = choice.Criteria,
            SecondCriteria = choice.SecondCriteria,
            Join = choice.Join,
            IncludeBlanks = choice.IncludeBlanks,
            Color = choice.Color,
        });

        Record(sheet, before);
        ApplyFilters(sheet);
    }

    private void Record(Worksheet sheet, AutoFilterMemento before)
    {
        var after = sheet.AutoFilter.Save();
        Context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["command.ledger.data.autoFilter.title"],
            redo: () => Reapply(sheet, after),
            undo: () => Reapply(sheet, before)));
    }

    private void Reapply(Worksheet sheet, AutoFilterMemento state)
    {
        sheet.AutoFilter.Restore(state);
        if (sheet.AutoFilter.IsActive) ApplyFilters(sheet);
        else rangeFilters.Refresh(Context, sheet);
    }

    private void Toggle()
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        if (Refused(sheet)) return;
        var before = sheet.AutoFilter.Save();

        if (sheet.AutoFilter.IsActive)
        {
            sheet.AutoFilter.Deactivate();
            rangeFilters.Refresh(Context, sheet);
            status.Report(texts["status.filter.off"]);
        }
        else
        {
            var range = Context.Selection.IsSingleCell
                ? regions.CurrentRegion(sheet, Context.Selection.Start)
                : Context.Selection;

            if (range.IsSingleCell)
            {
                status.Report(texts["status.filter.noData"], NotificationSeverity.Warning);
                return;
            }

            sheet.AutoFilter.Activate(range);
            status.Report(texts.Format("status.filter.on", range.ToString()));
        }

        Record(sheet, before);
        Context.RequestRefresh();
    }

    private void ApplyFilters(Worksheet sheet)
    {
        if (sheet.AutoFilter.Range is null) return;

        status.Report(texts.Format("status.filter.rows", rangeFilters.Refresh(Context, sheet)));
    }
}

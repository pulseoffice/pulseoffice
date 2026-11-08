using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using PulseOffice.Sheets.Styling;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.Drawing.Assets;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Search;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerEditDataCommands(
    ISheetFormattingService formatting,
    DataDialogs.LedgerSortServices sorting,
    ISheetRangeFilters rangeFilters,
    IActiveSheetContext context,
    ISheetClipboardService clipboard,
    IDialogService dialogs,
    IStatusBarService status,
    ISheetEditGuard guard,
    ISheetSortGate sortGate,
    PulseOffice.Shell.IDocumentWorkspace workspace,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. ClipboardCommands(),
        .. DataCommands(),
    ];

    private IEnumerable<CommandDefinition> ClipboardCommands() =>
    [
        Async("ledger.clipboard.copy", "clipboard", "Ctrl+C",
            ct => clipboard.CopyAsync(ct)),

        Async("ledger.clipboard.cut", "clipboard", "Ctrl+X",
            ct => clipboard.CutAsync(ct)),

        Async("ledger.clipboard.paste", "clipboard", "Ctrl+V",
            ct => clipboard.PasteAsync(PasteParts.All, ct), () => clipboard.HasContent),

        Async("ledger.clipboard.pasteValues", "clipboard", "Ctrl+Alt+Shift+V",
            ct => clipboard.PasteAsync(PasteParts.ValuesOnly, ct), () => clipboard.HasContent),

        Async("ledger.clipboard.pasteStyles", "clipboard", null,
            ct => clipboard.PasteAsync(PasteParts.StylesOnly, ct), () => clipboard.HasContent),
    ];

    private IEnumerable<CommandDefinition> DataCommands() =>
    [
        Data("ledger.data.sortAscending", "Ctrl+Shift+A",
            () => Sort(ascending: true)),

        Data("ledger.data.sortDescending", null,
            () => Sort(ascending: false)),

        Prototype("ledger.data.findReplace") with
        {
            MenuPath = "edit",
            DefaultGesture = "Ctrl+F",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => formatting.CanFormat,
            Execute = async (_, ct) => await dialogs.ShowAsync(
                new FindReplaceView(
                    new LedgerFindTarget(Context, status, texts, guard, new WorkbookSearch(), workspace.Active?.IsReadOnly == true),
                    texts),
                new DialogOptions
                {
                    TitleKey = "dialog.findReplace.title",
                    PreferredWidth = 480,
                    AccessibleDescription = texts["dialog.findReplace.description"],
                },
                ct),
        },
    ];

    private void Sort(bool ascending)
    {
        if (Context.Workbook is not { } workbook) return;

        if (sorting.QuickSort.Plan(workbook, Context.SheetIndex, Context.Selection) is not { } plan) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        if (sortGate.Refuses(workbook, sheet, plan.Range)) return;

        SortKey[] keys = [new SortKey(plan.Column, ascending)];
        var label = texts["history.ledger.sort"];
        using (Context.History?.BeginTransaction(label))
        {
            sorting.Sorter.Sort(sheet, plan.Range, keys, plan.HasHeader, Context.History, label);
            sorting.Memory.Remember(workbook, Context.SheetIndex, plan.Range, keys, Context.History, label, plan.HasHeader);
        }
        if (plan.ReappliesAutoFilter) rangeFilters.Refresh(Context, sheet);

        Context.RequestRefresh();
    }

    private CommandDefinition Async(
    string id, string group, string? gesture,
    Func<CancellationToken, Task> action, Func<bool>? enabled = null) =>
    Prototype(id) with
    {
        MenuPath = "edit",
        ForbiddenState = id.EndsWith("copy", StringComparison.Ordinal)
            ? DocumentState.None
            : DocumentState.ReadOnly,
        ContextPaths = group == "clipboard"
            ? ["cell/clipboard", "row/clipboard", "column/clipboard"]
            : [],
        DefaultGesture = gesture,
        Surface = group == "clipboard"
            ? CommandSurface.Common | CommandSurface.ContextMenu
            : CommandSurface.Common,
        RequiredState = DocumentState.DocumentOpen,
        EnabledWhen = _ => formatting.CanFormat && (enabled?.Invoke() ?? true),
        Execute = async (_, ct) => await action(ct),
    };

    private CommandDefinition Data(string id, string? gesture, Action action, Func<bool>? enabled = null) =>
    Prototype(id) with
    {
        MenuPath = "data",
        DefaultGesture = gesture,
        Surface = CommandSurface.Common,
        RequiredState = DocumentState.DocumentOpen,
        RequiredSelection = SelectionKind.CellRange,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => formatting.CanFormat && (enabled?.Invoke() ?? true),
        Execute = (_, _) =>
        {
            action();
            return Task.CompletedTask;
        },
    };
}

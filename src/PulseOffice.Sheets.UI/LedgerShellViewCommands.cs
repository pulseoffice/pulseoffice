using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public interface IShellViewOptions
{
    bool FormulaBarVisible { get; set; }

    bool SheetTabsVisible { get; set; }

    bool HeadersVisible { get; set; }
}

public sealed class LedgerShellViewCommands(
    IActiveSheetContext context,
    IShellViewOptions options) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Toggle("ledger.view.formulaBar", () => options.FormulaBarVisible, () => options.FormulaBarVisible = !options.FormulaBarVisible),
        Toggle("ledger.view.sheetTabs", () => options.SheetTabsVisible, () => options.SheetTabsVisible = !options.SheetTabsVisible),
        Toggle("ledger.view.headers", () => options.HeadersVisible, () => options.HeadersVisible = !options.HeadersVisible),
        Focus("ledger.view.focusNameBox", SheetInputArea.NameBox, "Ctrl+Shift+F5"),
        Focus("ledger.view.focusFormulaBar", SheetInputArea.FormulaBar, "Ctrl+Shift+F2"),
    ];

    private CommandDefinition Focus(string id, SheetInputArea area, string gesture) =>
        Prototype(id) with
        {
            MenuPath = "view/navigate",
            DefaultGesture = gesture,
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                Context.Focus(area);
                return Task.CompletedTask;
            },
        };

    private CommandDefinition Toggle(string id, Func<bool> isOn, Action run, string? gesture = null) =>
        Prototype(id) with
        {
            MenuPath = "view/show",
            DefaultGesture = gesture,
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            CheckedWhen = _ => Context.HasTarget && isOn(),
            Execute = (_, _) =>
            {
                run();
                return Task.CompletedTask;
            },
        };
}

using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerFormDialogCommands(
    IActiveSheetContext context,
    PulseOffice.UI.Controls.Forms.IFormControlDialogs formDialogs,
    LedgerMacroEventsSource macroEvents,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.form.properties") with
        {
            IconKey = "ledger.form.properties",
            MenuPath = "tools/forms",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Selected() is not null,
            Execute = (_, ct) => PropertiesAsync(ct),
        },

        Prototype("ledger.form.tabOrder") with
        {
            MenuPath = "tools/forms",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Controls().Count > 1,
            Execute = (_, ct) => TabOrderAsync(ct),
        },

    ];

    private PlacedObject? Selected() =>
        Context.SelectedObject is { Content: FormControlObject } placed ? placed : null;

    private List<PlacedObject> Controls() =>
        Context.Workbook is not { } workbook
            ? []
            : [.. workbook.Sheets[Context.SheetIndex].Objects.Where(placed => placed.Content is FormControlObject)];

    private async Task PropertiesAsync(CancellationToken ct)
    {
        if (Selected() is not { Content: FormControlObject control } placed) return;
        if (macroEvents.Create(readOnly: false) is not { } events) return;

        if (await formDialogs.EditAsync(control, events, "command.ledger.form.properties.title", ct).ConfigureAwait(true) is not { } properties) return;

        Apply([(placed, control with { Properties = properties })]);
    }

    private async Task TabOrderAsync(CancellationToken ct)
    {
        var controls = Controls();
        var names = await formDialogs.TabOrderAsync(
            [.. controls.Select(placed => ((FormControlObject)placed.Content).Properties)],
            "command.ledger.form.tabOrder.title", ct).ConfigureAwait(true);
        if (names is null) return;

        var order = names.Select((name, index) => (name, index)).ToDictionary(entry => entry.name, entry => entry.index);
        Apply([.. controls
            .Select(placed => (placed, Control: (FormControlObject)placed.Content))
            .Where(entry => order.ContainsKey(entry.Control.Properties.Name))
            .Select(entry => (entry.placed, entry.Control with
            {
                Properties = entry.Control.Properties with { TabIndex = order[entry.Control.Properties.Name] },
            }))]);
    }

    private void Apply(IReadOnlyList<(PlacedObject Placed, FormControlObject Content)> changes)
    {
        if (Context.Workbook is not { } workbook || changes.Count == 0) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var swaps = changes
            .Select(change => (Index: sheet.Objects.IndexOf(change.Placed), Before: change.Placed, After: change.Placed with { Content = change.Content }))
            .Where(swap => swap.Index >= 0)
            .ToList();

        void Set(bool forward)
        {
            foreach (var swap in swaps) sheet.Objects[swap.Index] = forward ? swap.After : swap.Before;
        }

        Set(true);
        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["history.form.control"], () => Set(true), () => Set(false)));

        if (swaps.Count == 1) Context.SelectObject(swaps[0].After);
        Context.RequestRefresh();
    }
}

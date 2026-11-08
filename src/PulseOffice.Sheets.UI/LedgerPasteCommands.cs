using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Exchange;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerPasteCommands(
    ISheetClipboardService clipboard,
    IObjectExchange exchange,
    IExchangePasteChooser chooser,
    IDialogService dialogs,
    DataDialogs.LedgerDialogKit kit,
    DataDialogs.IPasteSketcher sketcher)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Paste("ledger.clipboard.pasteTransposed", null,
            new PasteRequest { Transpose = true }),

        Paste("ledger.clipboard.pasteFormulas", null,
            new PasteRequest { Parts = PasteParts.Formulas | PasteParts.Values }),

        Paste("ledger.clipboard.pasteText", null,
            new PasteRequest { Parts = PasteParts.ValuesOnly }),

        Paste("ledger.clipboard.pasteLink", null,
            new PasteRequest { Link = true }),

        new CommandDefinition
        {
            Id = new CommandId("ledger.clipboard.pasteSpecial"),
            Module = ModuleKind.Sheets,
            MenuPath = "edit/clipboard",
            ContextPaths = ["cell/clipboard"],
            DefaultGesture = "Ctrl+Shift+V",
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => clipboard.HasContent,
            Execute = (_, ct) => SpecialAsync(ct),
        },
    ];

    private CommandDefinition Paste(string id, string? gesture, PasteRequest request) =>
        new()
        {
            Id = new CommandId(id),
            Module = ModuleKind.Sheets,
            MenuPath = "edit/clipboard",
            ContextPaths = ["cell/clipboard"],
            DefaultGesture = gesture,
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => clipboard.HasContent,
            Execute = (_, ct) => clipboard.PasteAsync(request, ct),
        };

    private async Task SpecialAsync(CancellationToken ct)
    {
        var package = await exchange.CurrentAsync(ct).ConfigureAwait(true);
        var modes = exchange.Modes(ModuleKind.Sheets, package);
        if (modes.Count == 0 || modes.Any(mode => mode is not (ExchangePasteMode.Native or ExchangePasteMode.Cells)))
        {
            if (await chooser.ChooseAsync(modes, "dialog.pasteSpecial.heading", ct).ConfigureAwait(true) is not { } chosen) return;
            if (chosen is not (ExchangePasteMode.Native or ExchangePasteMode.Cells))
            {
                exchange.Receive(ModuleKind.Sheets, package, chosen);
                return;
            }
        }

        var choice = await dialogs.ShowAsync<DataDialogs.PasteSpecialChoice>(
            new DataDialogs.PasteSpecialView(kit, sketcher),
            new DialogOptions { TitleKey = "dialog.pasteSpecial.heading" }, ct).ConfigureAwait(true);

        if (choice is null) return;

        await clipboard.PasteAsync(choice.Request(), ct).ConfigureAwait(true);
    }
}

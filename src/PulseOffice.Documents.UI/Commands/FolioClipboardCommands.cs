using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Exchange;
using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Exchange;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioClipboardCommands(
    IActiveFolioContext context,
    IObjectExchange exchange,
    ITablePasting tables,
    IExchangePasteChooser chooser) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Editing("folio.clipboard.pasteUnformatted") with { DefaultGesture = "Ctrl+Alt+Shift+V", Execute = (_, ct) => exchange.PasteAsync(ModuleKind.Documents, ExchangePasteMode.UnformattedText, ct) },
        Editing("folio.clipboard.pasteSpecial") with { DefaultGesture = "Ctrl+Shift+V", Execute = (_, ct) => SpecialAsync(ct) },
        Editing("folio.clipboard.pasteNestedTable") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { InTable: true } editor && !editor.ReadOnly() && tables.HoldsTable(Fragment),
            Execute = (_, _) => Run(editor => editor.Paste(Fragment!)),
        },
        Table("folio.clipboard.pasteRowsBefore", rows: true),
        Table("folio.clipboard.pasteColumnsBefore", rows: false),
    ];

    private DocumentFragment? Fragment => exchange.Last?.NativeFrom<FolioNative>(ModuleKind.Documents)?.Fragment;

    private CommandDefinition Table(string id, bool rows) =>
        Editing(id) with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { InTable: true } editor && !editor.ReadOnly() && tables.HoldsTable(Fragment),
            Execute = (_, _) => Run(editor =>
            {
                if (editor.Tables.Editing.Locate(editor.Document, editor.Caret.ParagraphId) is not { } at || Fragment is not { } fragment) return;
                editor.Change(() =>
                {
                    if (rows) tables.PasteRows(editor.Document, editor.History, at, fragment);
                    else tables.PasteColumns(editor.Document, editor.History, at, fragment);
                });
            }),
        };

    private Task Run(Action<FolioEditor> action)
    {
        if (Editor is { } editor) action(editor);
        return Task.CompletedTask;
    }

    private async Task SpecialAsync(CancellationToken ct)
    {
        if (Editor is null) return;
        var package = await exchange.CurrentAsync(ct).ConfigureAwait(true);
        var modes = exchange.Modes(ModuleKind.Documents, package);
        if (await chooser.ChooseAsync(modes, "command.folio.clipboard.pasteSpecial.title", ct).ConfigureAwait(true) is { } mode)
            exchange.Receive(ModuleKind.Documents, package, mode);
    }
}

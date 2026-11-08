using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI.Scripting;

public sealed class LedgerScriptHost(ISheetClipboardService clipboard, IScriptClipboardOperations operations) : IScriptHost
{
    public string ModuleName => "Ledger";

    public bool HasClipboard => clipboard.HasContent;

    public Task Copy() => operations.RunAsync("command.ledger.clipboard.copy.title", () => clipboard.CopyAsync());

    public Task Cut() => operations.RunAsync("command.ledger.clipboard.cut.title", () => clipboard.CutAsync());

    public Task Paste() => operations.RunAsync("command.ledger.clipboard.paste.title", () => clipboard.PasteAsync());

    public Task PasteValues() => operations.RunAsync("command.ledger.clipboard.paste.title", () => clipboard.PasteAsync(PasteParts.Values));
}

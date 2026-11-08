using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.UI.Exchange;

namespace PulseOffice.Sheets.UI;

public interface ISheetClipboardService
{
    bool HasContent { get; }

    Task CopyAsync(CancellationToken ct = default);
    Task CutAsync(CancellationToken ct = default);
    Task PasteAsync(PasteParts parts = PasteParts.All, CancellationToken ct = default);

    Task PasteAsync(PasteRequest request, CancellationToken ct = default);
}

public sealed class SheetClipboardService(IObjectExchange exchange, ILedgerExchange ledger) : ISheetClipboardService
{
    public bool HasContent => exchange.CanPaste(ModuleKind.Sheets);

    public Task CopyAsync(CancellationToken ct = default) => exchange.CopyAsync(ModuleKind.Sheets, ct);

    public Task CutAsync(CancellationToken ct = default) => exchange.CutAsync(ModuleKind.Sheets, ct);

    public Task PasteAsync(PasteParts parts = PasteParts.All, CancellationToken ct = default) =>
        PasteAsync(new PasteRequest { Parts = parts }, ct);

    public async Task PasteAsync(PasteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ledger.CanReceive) return;
        var package = await exchange.CurrentAsync(ct).ConfigureAwait(true);
        if (ledger.Modes(package) is [var mode, ..]) ledger.Receive(package, mode, request);
    }
}

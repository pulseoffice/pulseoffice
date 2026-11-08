using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public interface IExchangeDragDrop
{
    void AcceptDrops(Window window);

    void Attach(Control view, ModuleKind module);

    bool IsDragGesture(KeyModifiers modifiers);

    Task<bool> DropAsync(IAsyncDataTransfer transfer, CancellationToken ct = default);
}

public sealed class ExchangeDragDrop(
    IObjectExchange exchange,
    IExchangeDataTransfers transfers,
    IDocumentWorkspace workspace,
    ILogger<ExchangeDragDrop> logger) : IExchangeDragDrop
{
    private const KeyModifiers Gesture = KeyModifiers.Control | KeyModifiers.Alt;

    public void AcceptDrops(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        DragDrop.SetAllowDrop(window, true);
        window.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            if (e.Handled || e.DataTransfer.Contains(DataFormat.File) || Target() is null) return;
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        });
        window.AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            if (e.Handled || e.DataTransfer.Contains(DataFormat.File) || Target() is null || e.DataTransfer is not IAsyncDataTransfer transfer) return;
            e.Handled = true;
            await DropAsync(transfer).ConfigureAwait(true);
        });
        workspace.DocumentOpened += (_, document) => Attach(document.View, document.Module);
        foreach (var document in workspace.Documents) Attach(document.View, document.Module);
    }

    public void Attach(Control view, ModuleKind module)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.AddHandler(InputElement.PointerPressedEvent, async (_, e) =>
        {
            if (!IsDragGesture(e.KeyModifiers) || !exchange.CanCopy(module) || exchange.Capture(module) is not { IsEmpty: false } package) return;
            e.Handled = true;
            logger.LogInformation("Dragging exchange package {Id} out of {Module}", package.Id, module);
            await DragDrop.DoDragDropAsync(e, transfers.Write(exchange.Publishable(package)), DragDropEffects.Copy).ConfigureAwait(true);
        }, RoutingStrategies.Tunnel);
    }

    public bool IsDragGesture(KeyModifiers modifiers) => (modifiers & Gesture) == Gesture;

    public async Task<bool> DropAsync(IAsyncDataTransfer transfer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (Target() is not { } module) return false;
        var representations = await transfers.ReadAsync(transfer).ConfigureAwait(true);
        if (representations.Count == 0) return false;
        return exchange.Receive(module, exchange.Recognize(representations));
    }

    private ModuleKind? Target() =>
        workspace.Active is { IsReadOnly: false, Module: not ModuleKind.Shell } active && exchange.CanPaste(active.Module) ? active.Module : null;
}

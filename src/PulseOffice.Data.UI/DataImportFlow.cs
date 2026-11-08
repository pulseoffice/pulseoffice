using PulseOffice.Document.Data;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Data.UI;

public interface IDataImportFlow
{
    bool HasConnections { get; }

    Task<DataImportChoice?> ChooseAsync(CancellationToken ct = default);

    Task<DataImportChoice?> ChooseSourceAsync(CancellationToken ct = default);

    Task ManageConnectionsAsync(CancellationToken ct = default);
}

public sealed class DataImportFlow(
    IConnectionDirectory connections,
    DatabaseProviderRegistry providers,
    IDataTableLoader loader,
    IDialogService dialogs,
    PulseOffice.UI.Icons.IIconCatalog icons,
    ITextCatalog texts,
    IDiagnosticTexts diagnostics) : IDataImportFlow
{
    public bool HasConnections => connections.All.Count > 0;

    public async Task<DataImportChoice?> ChooseAsync(CancellationToken ct = default) =>
        await ConnectedAsync(ct).ConfigureAwait(true)
            ? await dialogs.ShowAsync<DataImportChoice>(
                new DataImportView(connections, providers, loader, texts, diagnostics, icons, InterfaceDensity.Compact),
                new DialogOptions { TitleKey = "data.import.title", PreferredWidth = 760 },
                ct).ConfigureAwait(true)
            : null;

    public async Task<DataImportChoice?> ChooseSourceAsync(CancellationToken ct = default) =>
        await ConnectedAsync(ct).ConfigureAwait(true)
            ? await dialogs.ShowAsync<DataImportChoice>(
                new DataImportView(connections, providers, loader, texts, diagnostics, icons, InterfaceDensity.Compact, "data.import.useSource"),
                new DialogOptions { TitleKey = "data.import.sourceTitle", PreferredWidth = 760 },
                ct).ConfigureAwait(true)
            : null;

    private async Task<bool> ConnectedAsync(CancellationToken ct)
    {
        if (HasConnections) return true;
        await ManageConnectionsAsync(ct).ConfigureAwait(true);
        return HasConnections;
    }

    public Task ManageConnectionsAsync(CancellationToken ct = default) =>
        dialogs.ShowAsync(
            new ConnectionManagerView(connections, providers, texts, icons, InterfaceDensity.Compact),
            new DialogOptions { TitleKey = "data.connections.title", PreferredWidth = 720 },
            ct);
}

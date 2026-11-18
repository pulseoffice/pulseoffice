using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Shapes;

public sealed record ConnectorSitesChoice(ConnectorSite Start, ConnectorSite End);

public interface IConnectorSitesDialog
{
    Task<ConnectorSitesChoice?> AskAsync(ConnectorSite start, ConnectorSite end, CancellationToken ct);
}

public sealed class ConnectorSitesDialog(IDialogService dialogs, ITextCatalog texts) : IConnectorSitesDialog
{
    private readonly ConnectorSite[] _sites =
        [ConnectorSite.Automatic, ConnectorSite.Left, ConnectorSite.Top, ConnectorSite.Right, ConnectorSite.Bottom];

    public async Task<ConnectorSitesChoice?> AskAsync(ConnectorSite start, ConnectorSite end, CancellationToken ct)
    {
        IReadOnlyList<string> labels = [.. _sites.Select(site => texts[$"connector.site.{site.ToString().ToLowerInvariant()}"])];
        var answers = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["connector.sites.explanation"],
            [
                new ChoiceField("start", texts["connector.sites.start"], labels, Array.IndexOf(_sites, start)),
                new ChoiceField("end", texts["connector.sites.end"], labels, Array.IndexOf(_sites, end)),
            ], texts),
            new DialogOptions { TitleKey = "dialog.connectorSites.title", PreferredWidth = 420 }, ct).ConfigureAwait(true);
        if (answers is null) return null;
        return new ConnectorSitesChoice(Site(answers, "start"), Site(answers, "end"));
    }

    private ConnectorSite Site(IReadOnlyDictionary<string, object> answers, string key) =>
        answers.GetValueOrDefault(key) is int index && index >= 0 && index < _sites.Length ? _sites[index] : ConnectorSite.Automatic;
}

using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Presentations.UI;

public sealed class DeckConnectorCommands(IActiveDeckContext context, IDeckConnectors connectors,
    IConnectorSitesDialog connectorSites) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Connector("deck.connector.straight", ConnectorKind.Straight),
        Connector("deck.connector.elbow", ConnectorKind.Elbow),
        Connector("deck.connector.curved", ConnectorKind.Curved),
        Prototype("deck.connector.sites") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => SelectedConnector() is not null,
            Execute = (_, ct) => ResiteAsync(ct),
        },
    ];

    private ObjectElement? SelectedConnector() =>
        Context.Target is { } t && t.CurrentSlide() is not null
        && t.SelectedMany().ToList() is [ObjectElement { Content: ConnectorObject } connector]
            ? connector
            : null;

    private async Task ResiteAsync(CancellationToken ct)
    {
        if (SelectedConnector() is not { Content: ConnectorObject connector } placed) return;
        if (await connectorSites.AskAsync(connector.Start.Site, connector.End.Site, ct).ConfigureAwait(true) is not { } choice) return;
        if (choice.Start == connector.Start.Site && choice.End == connector.End.Site) return;
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;
        if (connectors.Resite(slide, placed, choice.Start, choice.End) is not { } resited) return;

        t.Actions.ReplaceElement(slide, resited, "history.connector.sites");
        t.Select([resited]);
    }

    private CommandDefinition Connector(string id, ConnectorKind kind) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t
                && t.CurrentSlide() is not null
                && t.SelectedMany().OfType<ObjectElement>().Count() >= 2,
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide)
                {
                    var objects = t.SelectedMany().OfType<ObjectElement>().ToList();
                    if (objects.Count >= 2
                        && connectors.Connect(slide, objects[0], objects[1], Guid.NewGuid().ToString("N"), kind) is { } element)
                    {
                        t.Actions.InsertObject(slide, element);
                        t.Select([element]);
                    }
                }
                return Task.CompletedTask;
            },
        };
}

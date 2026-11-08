using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;

namespace PulseOffice.Presentations.UI;

public sealed class DeckConnectorCommands(IActiveDeckContext context, IDeckConnectors connectors) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Connector("deck.connector.straight", ConnectorKind.Straight),
        Connector("deck.connector.elbow", ConnectorKind.Elbow),
        Connector("deck.connector.curved", ConnectorKind.Curved),
    ];

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

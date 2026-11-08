using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Presentations;

public sealed class ElementIdRemap
{
    private readonly IReadOnlyDictionary<string, string> _map;

    public ElementIdRemap(IReadOnlyDictionary<string, string> map) => _map = map;

    public SlideElement Apply(SlideElement element) =>
        element is ObjectElement { Content: ConnectorObject connector } placed
            ? placed with
            {
                Content = connector with
                {
                    Start = Remap(connector.Start),
                    End = Remap(connector.End),
                },
            }
            : element;

    private ConnectorEndpoint Remap(ConnectorEndpoint endpoint) =>
        endpoint.ObjectId is { } id && _map.TryGetValue(id, out var mapped)
            ? endpoint with { ObjectId = mapped }
            : endpoint;
}

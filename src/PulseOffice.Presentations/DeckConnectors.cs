using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Presentations;

public interface IDeckConnectors
{
    ObjectElement? Connect(Slide slide, ObjectElement from, ObjectElement to, string id, ConnectorKind kind,
        ConnectorSite fromSite = ConnectorSite.Automatic, ConnectorSite toSite = ConnectorSite.Automatic);

    bool Reroute(Slide slide);
}

public sealed class DeckConnectors(IConnectorFramer framer) : IDeckConnectors
{
    public ObjectElement? Connect(Slide slide, ObjectElement from, ObjectElement to, string id, ConnectorKind kind,
        ConnectorSite fromSite = ConnectorSite.Automatic, ConnectorSite toSite = ConnectorSite.Automatic)
    {
        ArgumentNullException.ThrowIfNull(slide);
        if (ReferenceEquals(from, to) || from.Id == to.Id) return null;

        var connector = new ConnectorObject
        {
            Id = id,
            Shape = kind,
            Start = new ConnectorEndpoint(from.Id, fromSite),
            End = new ConnectorEndpoint(to.Id, toSite),
        };

        var z = slide.Elements.Count == 0 ? 0 : slide.Elements.Max(e => e.ZOrder) + 1;
        return Reroute(slide, new ObjectElement { Id = id, ZOrder = z, Content = connector });
    }

    public bool Reroute(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        var changed = false;

        foreach (var element in slide.Elements.OfType<ObjectElement>()
            .Where(e => e.Content is ConnectorObject).ToList())
        {
            if (Reroute(slide, element) is { } updated)
            {
                slide.Replace(updated);
                changed = true;
            }
        }

        return changed;
    }

    private ObjectElement? Reroute(Slide slide, ObjectElement placed)
    {
        if (placed.Content is not ConnectorObject connector) return null;
        if (Find(slide, connector.Start.ObjectId) is not { } from) return null;
        if (Find(slide, connector.End.ObjectId) is not { } to) return null;

        if (framer.Frame(connector, Box(from), Box(to)) is not { } framed) return null;

        var bounds = new DocumentRect(
            new Twip((int)Math.Round(framed.Left)), new Twip((int)Math.Round(framed.Top)),
            new Twip((int)Math.Round(framed.Width)), new Twip((int)Math.Round(framed.Height)));

        return placed with { Bounds = bounds, Content = connector with { Path = framed.Path } };
    }

    private ConnectorBox Box(ObjectElement element) => new(
        element.Bounds.Left.Value, element.Bounds.Top.Value,
        element.Bounds.Width.Value, element.Bounds.Height.Value);

    private ObjectElement? Find(Slide slide, string? id) =>
        id is null ? null : slide.Elements.OfType<ObjectElement>().FirstOrDefault(e => e.Id == id);
}

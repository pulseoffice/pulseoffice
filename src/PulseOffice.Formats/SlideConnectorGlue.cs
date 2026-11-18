using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;

namespace PulseOffice.Formats;

public sealed class SlideConnectorGlue
{
    public void Resolve(Slide slide, Func<string, string?> elementOf, Func<SlideElement, bool> keepsSites)
    {
        ArgumentNullException.ThrowIfNull(slide);
        foreach (var element in slide.Elements.OfType<ObjectElement>().Where(element => element.Content is ConnectorObject).ToList())
        {
            var connector = (ConnectorObject)element.Content;
            var start = Glued(slide, connector.Start, element.Id, elementOf, keepsSites);
            var end = Glued(slide, connector.End, element.Id, elementOf, keepsSites);
            if (start == connector.Start && end == connector.End) continue;
            slide.Replace(element with { Content = connector with { Start = start, End = end } });
        }
    }

    private ConnectorEndpoint Glued(Slide slide, ConnectorEndpoint endpoint, string self,
        Func<string, string?> elementOf, Func<SlideElement, bool> keepsSites)
    {
        if (endpoint.ObjectId is not { Length: > 0 } raw || elementOf(raw) is not { } id || id == self) return ConnectorEndpoint.Free;
        if (slide.Elements.FirstOrDefault(candidate => candidate.Id == id) is not { } target) return ConnectorEndpoint.Free;
        return new ConnectorEndpoint(id, keepsSites(target) ? endpoint.Site : ConnectorSite.Automatic);
    }
}

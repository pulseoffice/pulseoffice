using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfGraphicStyles(IGraphicStyleRelinker relinker)
{
    public OdfGraphicStyles() : this(new GraphicStyleRelinker())
    {
    }

    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;

    public string? Encode(GraphicStyleCatalog catalog, string? name)
    {
        if (name is null) return null;
        var index = catalog.All.ToList().FindIndex(style => style.Name == name);
        return index < 0 ? null : $"QS{index + 1}";
    }

    public IEnumerable<XElement> Build(GraphicStyleCatalog catalog, OdfShapeStyleMapper mapper)
    {
        for (var index = 0; index < catalog.All.Count; index++)
        {
            var style = catalog.All[index];
            var element = mapper.Build($"QS{index + 1}", style.Style);
            element.Add(new XAttribute(Style + "display-name", style.Name));
            yield return element;
        }
    }

    public void Read(XDocument? styles, PulseOffice.Presentations.Presentation deck)
    {
        var root = styles?.Root?.Element(OdfPackage.Namespaces.Office + "styles");
        if (root is null) return;

        var definitions = root.Elements()
            .Where(element => element.Attribute(OdfPackage.Namespaces.Draw + "name") is not null)
            .GroupBy(element => (string)element.Attribute(OdfPackage.Namespaces.Draw + "name")!)
            .ToDictionary(group => group.Key, group => group.First());
        var reader = new OdfShapeStyleReader();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var element in root.Elements(Style + "style").Where(element => (string?)element.Attribute(Style + "family") == "graphic"))
        {
            if ((string?)element.Attribute(Style + "name") is not { } encoded) continue;
            var display = (string?)element.Attribute(Style + "display-name") ?? encoded;
            if (element.Element(Style + "graphic-properties") is null) continue;
            deck.GraphicStyles.Set(new GraphicStyle(display, reader.Read(element, definitions)));
            names[encoded] = display;
        }

        relinker.Relink(deck, styled => styled.StyleName is { } encoded
            ? styled.With(styled.Style, names.TryGetValue(encoded, out var display) ? display : null)
            : null);
    }
}

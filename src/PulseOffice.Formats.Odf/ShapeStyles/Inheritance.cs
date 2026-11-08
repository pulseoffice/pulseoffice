using System.Xml.Linq;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

public sealed class OdfGraphicStyleInheritance
{
    private const string Prefix = "graphic-style:";

    public string Key(string styleName) => Prefix + styleName;

    public XElement Effective(XElement style, IReadOnlyDictionary<string, XElement> definitions)
    {
        var chain = new List<XElement> { style };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = style;
        while ((string?)current.Attribute(Namespaces.Style + "parent-style-name") is { } parent
            && seen.Add(parent) && definitions.TryGetValue(Key(parent), out var ancestor))
        {
            chain.Add(ancestor);
            current = ancestor;
        }

        if (chain.Count == 1) return style;

        var merged = new XElement(Namespaces.Style + "graphic-properties");
        foreach (var level in Enumerable.Reverse(chain))
        {
            if (level.Element(Namespaces.Style + "graphic-properties") is not { } properties) continue;
            foreach (var attribute in properties.Attributes()) merged.SetAttributeValue(attribute.Name, attribute.Value);
            foreach (var child in properties.Elements())
            {
                merged.Elements(child.Name).Remove();
                merged.Add(new XElement(child));
            }
        }

        var result = new XElement(style.Name, style.Attributes(), merged);
        foreach (var other in style.Elements().Where(element => element.Name != Namespaces.Style + "graphic-properties"))
            result.Add(new XElement(other));
        return result;
    }
}

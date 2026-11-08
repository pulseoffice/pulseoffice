using System.Xml.Linq;

namespace PulseOffice.Formats.Odf;

internal abstract class AutomaticStyleTable<TValue>(string family)
{
    protected static readonly XNamespace Office = OdfPackage.Namespaces.Office;
    protected static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    protected static readonly XNamespace Fo = OdfPackage.Namespaces.Fo;
    protected static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    public IReadOnlyDictionary<string, TValue> Read(XElement root)
    {
        var result = new Dictionary<string, TValue>(StringComparer.Ordinal);
        if (root.Element(Office + "automatic-styles") is not { } automatic) return result;

        foreach (var style in automatic.Elements(Style + "style")
            .Where(s => (string?)s.Attribute(Style + "family") == family))
            if ((string?)style.Attribute(Style + "name") is { } name && Describe(style, automatic) is { } value)
                result[name] = value;

        return result;
    }

    protected abstract TValue? Describe(XElement style, XElement automatic);
}

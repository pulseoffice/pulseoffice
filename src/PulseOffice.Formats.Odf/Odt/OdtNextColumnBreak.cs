using System.Xml.Linq;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtNextColumnBreak : OdtXml
{
    public bool Place(XElement section, OdtStyleSink sink)
    {
        var leading = Leading(section);
        if (leading is null) return false;
        if (leading.Name == Table + "table") return Restyle(leading, Table + "style-name", "table", "table-properties", "Tb", sink);
        if (leading.Name == Text + "p" || leading.Name == Text + "h") return Restyle(leading, Text + "style-name", "paragraph", "paragraph-properties", "P", sink);
        return false;
    }

    private XElement? Leading(XElement container)
    {
        var first = container.Elements().FirstOrDefault(element => element.Name != Text + "section-source");
        return first is not null && first.Name == Text + "section" ? Leading(first) : first;
    }

    private bool Restyle(XElement element, XName attribute, string family, string propertiesName, string prefix, OdtStyleSink sink)
    {
        var name = (string?)element.Attribute(attribute);
        var existing = name is null ? null : sink.Styles.FirstOrDefault(style =>
            (string?)style.Attribute(Style + "name") == name && (string?)style.Attribute(Style + "family") == family);
        if (existing?.Attribute(Style + "master-page-name") is not null) return false;
        var parent = existing is null ? name : (string?)existing.Attribute(Style + "parent-style-name");
        var attributes = existing?.Attributes().Where(own => own.Name != Style + "name" && own.Name != Style + "family" && own.Name != Style + "parent-style-name")
            .Select(own => new XAttribute(own)).ToList() ?? [];
        var children = existing?.Elements().Select(child => new XElement(child)).ToList() ?? [];
        var properties = children.FirstOrDefault(child => child.Name == Style + propertiesName);
        if (properties is null)
        {
            properties = new XElement(Style + propertiesName);
            children.Insert(0, properties);
        }
        if ((string?)properties.Attribute(Fo + "break-before") is { } current && current != "auto") return false;
        properties.SetAttributeValue(Fo + "break-before", "column");
        element.SetAttributeValue(attribute, sink.Register(family, parent, [.. attributes, .. children], prefix));
        return true;
    }
}

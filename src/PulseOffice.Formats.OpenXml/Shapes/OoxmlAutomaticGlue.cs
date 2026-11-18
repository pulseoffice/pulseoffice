using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlAutomaticGlue
{
    private const string Uri = "{3E7A0C51-8B2F-4D6A-9C14-7F0D2B5E9A31}";
    private const string Namespace = "urn:pulseoffice:connector-glue";
    private const string DrawingNamespace = "http://schemas.openxmlformats.org/drawingml/2006/main";

    public void Write(OpenXmlCompositeElement visual, ConnectorObject connector, bool startGlued, bool endGlued)
    {
        var start = startGlued && connector.Start.Site == ConnectorSite.Automatic;
        var end = endGlued && connector.End.Site == ConnectorSite.Automatic;
        if (!start && !end) return;

        var marker = new OpenXmlUnknownElement("pg", "automatic", Namespace);
        if (start) marker.SetAttribute(new OpenXmlAttribute("start", string.Empty, "1"));
        if (end) marker.SetAttribute(new OpenXmlAttribute("end", string.Empty, "1"));
        var extension = new OpenXmlUnknownElement("a", "ext", DrawingNamespace);
        extension.SetAttribute(new OpenXmlAttribute("uri", string.Empty, Uri));
        extension.AppendChild(marker);
        var extensions = new OpenXmlUnknownElement("a", "extLst", DrawingNamespace);
        extensions.AppendChild(extension);
        visual.AppendChild(extensions);
    }

    public ConnectorObject Read(ConnectorObject connector, OpenXmlElement? visual)
    {
        var marker = visual?.Descendants().FirstOrDefault(element => element.LocalName == "automatic" && element.NamespaceUri == Namespace);
        if (marker is null) return connector;
        bool Flag(string name) => marker.GetAttributes().Any(attribute => attribute.LocalName == name && attribute.Value == "1");
        return connector with
        {
            Start = Flag("start") && connector.Start.IsAttached ? connector.Start with { Site = ConnectorSite.Automatic } : connector.Start,
            End = Flag("end") && connector.End.IsAttached ? connector.End with { Site = ConnectorSite.Automatic } : connector.End,
        };
    }
}

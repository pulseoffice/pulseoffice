using DocumentFormat.OpenXml;
using PulseOffice.Charts;
using PulseOffice.Sheets.Engine.Operations;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxChartTableSources(IChartTableSources tables)
{
    private const string Uri = "{6A2D9C41-3F7B-4E15-8B0C-7D4E2A91F356}";
    private const string Namespace = "urn:pulseoffice:chart";
    private const string Prefix = "pulseoffice";

    public XlsxChartTableSources() : this(new ChartTableSources()) { }

    public void Write(C.ChartSpace space, ChartDefinition structured)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(structured);
        if (!tables.ReadsTables(structured)) return;

        var list = new OpenXmlUnknownElement(Prefix, "tableSources", Namespace);
        foreach (var source in structured.SeriesSources!)
        {
            var element = new OpenXmlUnknownElement(Prefix, "source", Namespace);
            if (source.Name is { } name) element.SetAttribute(new OpenXmlAttribute("name", string.Empty, name));
            if (source.Values is { } values) element.SetAttribute(new OpenXmlAttribute("values", string.Empty, values));
            if (source.Categories is { } categories) element.SetAttribute(new OpenXmlAttribute("categories", string.Empty, categories));
            list.AppendChild(element);
        }

        var extensions = space.GetFirstChild<C.ChartSpaceExtensionList>() ?? space.AppendChild(new C.ChartSpaceExtensionList());
        var extension = new C.ChartSpaceExtension { Uri = Uri };
        extension.AppendChild(list);
        extensions.AppendChild(extension);
    }

    public IReadOnlyList<ChartSeriesSource>? Read(C.ChartSpace space)
    {
        ArgumentNullException.ThrowIfNull(space);
        var list = space.GetFirstChild<C.ChartSpaceExtensionList>()?.Elements<C.ChartSpaceExtension>()
            .Where(extension => extension.Uri?.Value == Uri)
            .SelectMany(extension => extension.ChildElements)
            .FirstOrDefault(child => child.LocalName == "tableSources" && child.NamespaceUri == Namespace);
        if (list is null) return null;

        var sources = list.ChildElements.Where(child => child.LocalName == "source")
            .Select(child => new ChartSeriesSource(Attribute(child, "name"), Attribute(child, "values"), Attribute(child, "categories")))
            .ToList();
        return sources.Count > 0 ? sources : null;
    }

    private string? Attribute(OpenXmlElement element, string name) =>
        element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == name).Value is { Length: > 0 } value ? value : null;
}

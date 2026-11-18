using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfChartTableSources(IChartTableSources tables)
{
    private readonly XNamespace _pulse = "urn:pulseoffice:spreadsheet";

    public OdfChartTableSources() : this(new ChartTableSources()) { }

    public void Write(XDocument content, ChartDefinition structured)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(structured);
        if (!tables.ReadsTables(structured)) return;
        if (content.Descendants(PulseOffice.Formats.Packaging.OdfPackage.Namespaces.Chart + "chart").FirstOrDefault() is not { } chart) return;

        chart.Add(new XElement(_pulse + "table-sources",
            new XAttribute(XNamespace.Xmlns + "pt", _pulse.NamespaceName),
            structured.SeriesSources!.Select(source => new XElement(_pulse + "source",
                source.Name is null ? null : new XAttribute(_pulse + "name", source.Name),
                source.Values is null ? null : new XAttribute(_pulse + "values", source.Values),
                source.Categories is null ? null : new XAttribute(_pulse + "categories", source.Categories)))));
    }

    public IReadOnlyList<ChartSeriesSource>? Read(XElement chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        if (chart.Element(_pulse + "table-sources") is not { } list) return null;
        var sources = list.Elements(_pulse + "source")
            .Select(source => new ChartSeriesSource(Text(source, "name"), Text(source, "values"), Text(source, "categories")))
            .ToList();
        return sources.Count > 0 ? sources : null;
    }

    private string? Text(XElement element, string name) =>
        (string?)element.Attribute(_pulse + name) is { Length: > 0 } value ? value : null;
}

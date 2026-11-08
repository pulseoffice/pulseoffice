using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfExternalDataVocabulary
{
    public TokenMap<ExternalDataFormat> Filters { get; } = new(ExternalDataFormat.Html, "calc_HTML_WebQuery",
        (ExternalDataFormat.Csv, "Text - txt - csv (StarCalc)"),
        (ExternalDataFormat.Html, "calc_HTML_WebQuery"),
        (ExternalDataFormat.Xml, "calc_XML_Source"));

    public TokenMap<ExternalDataFormat> Providers { get; } = new(ExternalDataFormat.Csv, "org.libreoffice.calc.csv",
        (ExternalDataFormat.Csv, "org.libreoffice.calc.csv"),
        (ExternalDataFormat.Html, "org.libreoffice.calc.html"),
        (ExternalDataFormat.Xml, "org.libreoffice.calc.xml"));
}

internal sealed class OdfExternalDataMapper
{
    private readonly OdfExternalDataVocabulary _vocabulary = new();
    private readonly OdfQualifiedRanges _ranges = new();
    private readonly TableCells _cells = new();
    private readonly OdfLinkSyntax _links = new();

    public Dictionary<CellAddress, List<XElement>> AreaLinkSources(Workbook workbook, int sheetIndex)
    {
        var sources = new Dictionary<CellAddress, List<XElement>>();
        foreach (var link in workbook.ExternalData.AreaLinks.Where(link => link.SheetIndex == sheetIndex))
        {
            var source = new XElement(Namespaces.Table + "cell-range-source",
                new XAttribute(Namespaces.Table + "name", link.Source),
                new XAttribute(Namespaces.Xlink + "type", "simple"),
                new XAttribute(Namespaces.Xlink + "href", _links.ToOdf(link.Url)),
                new XAttribute(Namespaces.Table + "filter-name", _vocabulary.Filters.Token(link.Format)),
                new XAttribute(Namespaces.Table + "last-column-spanned", Math.Max(link.Columns, 1).ToString(CultureInfo.InvariantCulture)),
                new XAttribute(Namespaces.Table + "last-row-spanned", Math.Max(link.Rows, 1).ToString(CultureInfo.InvariantCulture)));
            if (link.RefreshSeconds > 0)
                source.SetAttributeValue(Namespaces.Table + "refresh-delay", XmlConvert.ToString(TimeSpan.FromSeconds(link.RefreshSeconds)));
            if (!sources.TryGetValue(link.Anchor, out var list)) sources[link.Anchor] = list = [];
            list.Add(source);
        }
        return sources;
    }

    public IEnumerable<XElement> BuildWorkbookElements(Workbook workbook)
    {
        if (workbook.ExternalData.Stream is { } stream
            && stream.SheetIndex >= 0 && stream.SheetIndex < workbook.Sheets.Count)
            yield return new XElement(Namespaces.Calcext + "data-stream-source",
                new XAttribute(Namespaces.Xlink + "href", _links.ToOdf(stream.Url)),
                new XAttribute(Namespaces.Table + "target-range-address", _ranges.Write(workbook.Sheets[stream.SheetIndex].Name, stream.Range)),
                new XAttribute(Namespaces.Calcext + "empty-line-refresh", stream.RefreshOnEmptyLine ? "true" : "false"),
                new XAttribute(Namespaces.Calcext + "insertion-position", stream.Insertion == StreamInsertion.Top ? "top" : "bottom"));
    }

    public XElement? BuildMappings(Workbook workbook)
    {
        if (workbook.ExternalData.Mappings.Count == 0) return null;

        return new XElement(Namespaces.Calcext + "data-mappings",
            workbook.ExternalData.Mappings.Select(mapping => new XElement(Namespaces.Calcext + "data-mapping",
                new XAttribute(Namespaces.Xlink + "href", _links.ToOdf(mapping.Url)),
                new XAttribute(Namespaces.Calcext + "provider", _vocabulary.Providers.Token(mapping.Provider)),
                new XAttribute(Namespaces.Calcext + "data-frequency", "10"),
                new XAttribute(Namespaces.Calcext + "id", mapping.Id),
                new XAttribute(Namespaces.Calcext + "database-name", mapping.DatabaseName))));
    }

    public void Read(XElement root, Workbook workbook)
    {
        foreach (var table in root.Descendants(Namespaces.Table + "table"))
        {
            var name = (string?)table.Attribute(Namespaces.Table + "name") ?? string.Empty;
            var sheetIndex = IndexOf(workbook, name);
            if (sheetIndex < 0) continue;

            foreach (var cell in _cells.In(table))
            {
                if (cell.Element.Element(Namespaces.Table + "cell-range-source") is not { } source) continue;
                if ((string?)source.Attribute(Namespaces.Xlink + "href") is not { Length: > 0 } url) continue;

                workbook.ExternalData.Replace(new AreaLink
                {
                    SheetIndex = sheetIndex,
                    Anchor = cell.Address,
                    Url = _links.FromOdf(url),
                    Format = _vocabulary.Filters.Value((string?)source.Attribute(Namespaces.Table + "filter-name")),
                    Source = (string?)source.Attribute(Namespaces.Table + "name") ?? string.Empty,
                    Columns = Count(source, "last-column-spanned", CellAddress.MaxColumns),
                    Rows = Count(source, "last-row-spanned", CellAddress.MaxRows),
                    RefreshSeconds = Delay((string?)source.Attribute(Namespaces.Table + "refresh-delay")),
                });
            }
        }

        if (root.Descendants(Namespaces.Calcext + "data-stream-source").FirstOrDefault() is { } stream
            && (string?)stream.Attribute(Namespaces.Xlink + "href") is { Length: > 0 } streamUrl
            && _ranges.Read((string?)stream.Attribute(Namespaces.Table + "target-range-address")) is { } target
            && IndexOf(workbook, target.Sheet) is var streamSheet && streamSheet >= 0)
            workbook.ExternalData.Stream = new DataStreamSource
            {
                Url = _links.FromOdf(streamUrl),
                SheetIndex = streamSheet,
                Range = target.Range,
                Insertion = (string?)stream.Attribute(Namespaces.Calcext + "insertion-position") == "top" ? StreamInsertion.Top : StreamInsertion.Bottom,
                RefreshOnEmptyLine = (string?)stream.Attribute(Namespaces.Calcext + "empty-line-refresh") == "true",
            };

        foreach (var mapping in root.Descendants(Namespaces.Calcext + "data-mapping"))
        {
            if ((string?)mapping.Attribute(Namespaces.Xlink + "href") is not { Length: > 0 } url) continue;
            if ((string?)mapping.Attribute(Namespaces.Calcext + "database-name") is not { Length: > 0 } database) continue;

            workbook.ExternalData.Replace(new DataMapping
            {
                Id = (string?)mapping.Attribute(Namespaces.Calcext + "id") ?? string.Empty,
                Url = _links.FromOdf(url),
                Provider = _vocabulary.Providers.Value((string?)mapping.Attribute(Namespaces.Calcext + "provider")),
                DatabaseName = database,
            });
        }
    }

    public const int MaximumRefreshSeconds = 86_400 * 7;

    private int Count(XElement source, string attribute, int maximum) =>
        int.TryParse((string?)source.Attribute(Namespaces.Table + attribute), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? Math.Min(value, maximum)
            : 1;

    private int Delay(string? text)
    {
        if (text is not { Length: > 0 }) return 0;
        try
        {
            var seconds = XmlConvert.ToTimeSpan(text.Trim()).TotalSeconds;
            return double.IsFinite(seconds) && seconds >= 1 ? (int)Math.Min(seconds, MaximumRefreshSeconds) : 0;
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            return 0;
        }
    }

    private int IndexOf(Workbook workbook, string name)
    {
        for (var index = 0; index < workbook.Sheets.Count; index++)
            if (string.Equals(workbook.Sheets[index].Name, name, StringComparison.Ordinal)) return index;
        return -1;
    }
}

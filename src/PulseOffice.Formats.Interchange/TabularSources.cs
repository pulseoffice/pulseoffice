using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using PulseOffice.Foundation.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Formats.Interchange;

public sealed record TabularData(IReadOnlyList<IReadOnlyList<string>> Rows)
{
    public static TabularData Empty { get; } = new([]);

    public int RowCount => Rows.Count;

    public int ColumnCount => Rows.Count == 0 ? 0 : Rows.Max(row => row.Count);
}

public interface ITabularSource
{
    ExternalDataFormat Format { get; }

    IReadOnlyList<string> Tables(string text);

    TabularData Read(string text, string table);
}

public interface ITabularSources
{
    ITabularSource For(ExternalDataFormat format);

    ExternalDataFormat Guess(string url);
}

public sealed class TabularSources : ITabularSources
{
    private readonly Dictionary<ExternalDataFormat, ITabularSource> _sources;

    public TabularSources(IEnumerable<ITabularSource> sources)
    {
        _sources = sources.ToDictionary(source => source.Format);
        foreach (var format in Enum.GetValues<ExternalDataFormat>())
            if (!_sources.ContainsKey(format))
                throw new InvalidOperationException($"No tabular source registered for {format}.");
    }

    public TabularSources(IDelimitedText delimited)
        : this([new CsvTabularSource(delimited), new HtmlTabularSource(), new XmlTabularSource()])
    {
    }

    public ITabularSource For(ExternalDataFormat format) => _sources[format];

    public ExternalDataFormat Guess(string url)
    {
        var path = url.Split('?', '#')[0];
        var extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".csv" or ".txt" or ".tsv" => ExternalDataFormat.Csv,
            ".xml" => ExternalDataFormat.Xml,
            _ => ExternalDataFormat.Html,
        };
    }
}

public sealed class CsvTabularSource(IDelimitedText delimited) : ITabularSource
{
    public ExternalDataFormat Format => ExternalDataFormat.Csv;

    public IReadOnlyList<string> Tables(string text) => [string.Empty];

    public TabularData Read(string text, string table)
    {
        var sample = text.Length > 4096 ? text[..4096] : text;
        var separator = delimited.GuessSeparator(sample) ?? ',';
        var format = new DelimitedFormat { Separator = separator };

        using var reader = new StringReader(text);
        return new TabularData([.. delimited.Read(reader, format).Where(row => row.Count > 1 || row.Any(field => field.Length > 0))]);
    }
}

public sealed class HtmlTabularSource : ITabularSource
{
    public const string AllTables = "HTML_all";

    private const string TablePrefix = "HTML_";

    private readonly Regex _tables = new(@"<table\b[^>]*>(.*?)</table\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
    private readonly Regex _rows = new(@"<tr\b[^>]*>(.*?)(?=<tr\b|</table|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
    private readonly Regex _cells = new(@"<t([dh])\b([^>]*)>(.*?)(?=<t[dh]\b|</tr|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
    private readonly Regex _span = new(@"colspan\s*=\s*[""']?(\d+)", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    private readonly Regex _tags = new(@"<[^>]*>", RegexOptions.Singleline, TimeSpan.FromSeconds(2));
    private readonly Regex _breaks = new(@"<br\s*/?>", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    private readonly Regex _spaces = new(@"[ \t\r\n ]+", RegexOptions.None, TimeSpan.FromSeconds(1));

    public ExternalDataFormat Format => ExternalDataFormat.Html;

    public IReadOnlyList<string> Tables(string text)
    {
        var count = Guarded(() => _tables.Matches(text).Count);
        return count == 0 ? [] : [AllTables, .. Enumerable.Range(1, count).Select(index => TablePrefix + index)];
    }

    public TabularData Read(string text, string table)
    {
        return Guarded(() => ReadTables(text, table));
    }

    private T Guarded<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new InvalidDataException("The HTML source is too complex to read.", exception);
        }
    }

    private TabularData ReadTables(string text, string table)
    {
        var bodies = _tables.Matches(text).Select(match => match.Groups[1].Value).ToList();
        if (bodies.Count == 0) return TabularData.Empty;

        if (table == AllTables || table.Length == 0)
            return new TabularData([.. bodies.SelectMany(RowsOf)]);

        return int.TryParse(table.StartsWith(TablePrefix, StringComparison.OrdinalIgnoreCase) ? table[TablePrefix.Length..] : table,
                   System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index)
               && index >= 1 && index <= bodies.Count
            ? new TabularData([.. RowsOf(bodies[index - 1])])
            : TabularData.Empty;
    }

    private IEnumerable<IReadOnlyList<string>> RowsOf(string body)
    {
        foreach (Match row in _rows.Matches(body))
        {
            var cells = new List<string>();
            foreach (Match cell in _cells.Matches(row.Groups[1].Value))
            {
                var span = _span.Match(cell.Groups[2].Value) is { Success: true } found ? Math.Clamp(int.Parse(found.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1, 64) : 1;
                cells.Add(Clean(cell.Groups[3].Value));
                for (var extra = 1; extra < span; extra++) cells.Add(string.Empty);
            }
            if (cells.Count > 0) yield return cells;
        }
    }

    private string Clean(string html)
    {
        var withoutEnd = Regex.Replace(html, @"</t[dh]\s*>.*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
        var text = _tags.Replace(_breaks.Replace(withoutEnd, " "), string.Empty);
        return _spaces.Replace(WebUtility.HtmlDecode(text), " ").Trim();
    }
}

public sealed class XmlTabularSource : ITabularSource
{
    private const long MaximumCells = 10_000_000;

    public ExternalDataFormat Format => ExternalDataFormat.Xml;

    public IReadOnlyList<string> Tables(string text)
    {
        var document = Load(text);
        if (document?.Root is null) return [];

        return [.. document.Root.DescendantsAndSelf()
            .Where(element => element.HasElements)
            .SelectMany(parent => parent.Elements()
                .GroupBy(child => child.Name)
                .Where(group => group.Skip(1).Any())
                .Select(group => group.First()))
            .Select(PathOf)
            .Distinct(StringComparer.Ordinal)];
    }

    public TabularData Read(string text, string table)
    {
        var document = Load(text);
        if (document?.Root is null) return TabularData.Empty;

        List<XElement> records;
        try
        {
            records = [.. document.XPathSelectElements(table.Length == 0 ? "/*/*" : table)];
        }
        catch (Exception exception) when (exception is XPathException or InvalidOperationException or NotSupportedException)
        {
            return TabularData.Empty;
        }
        if (records.Count == 0) return TabularData.Empty;

        var columns = new List<string>();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            foreach (var attribute in record.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
                if (known.Add("@" + attribute.Name.LocalName)) columns.Add("@" + attribute.Name.LocalName);
            foreach (var child in record.Elements())
                if (known.Add(child.Name.LocalName)) columns.Add(child.Name.LocalName);
            if (columns.Count > CellAddress.MaxColumns) throw TooLarge();
        }
        if (columns.Count == 0) columns.Add(records[0].Name.LocalName);
        if (records.Count >= CellAddress.MaxRows || (long)records.Count * columns.Count > MaximumCells) throw TooLarge();

        IReadOnlyList<string> header = [.. columns.Select(column => column.TrimStart('@'))];
        var rows = new List<IReadOnlyList<string>> { header };
        foreach (var record in records)
            rows.Add([.. columns.Select(column => ValueOf(record, column))]);
        return new TabularData(rows);
    }

    private InvalidDataException TooLarge() => new("The XML source is too large to read as a table.");

    private string ValueOf(XElement record, string column)
    {
        if (column.StartsWith('@'))
            return record.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == column[1..])?.Value ?? string.Empty;
        if (!record.HasElements && record.Name.LocalName == column) return record.Value.Trim();
        return record.Elements().FirstOrDefault(child => child.Name.LocalName == column)?.Value.Trim() ?? string.Empty;
    }

    private string PathOf(XElement element) =>
        "/" + string.Join("/", element.AncestorsAndSelf().Reverse().Select(ancestor => ancestor.Name.LocalName));

    private XDocument? Load(string text)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var document = XDocument.Load(reader);
            foreach (var element in document.Descendants().ToList())
            {
                element.Name = element.Name.LocalName;
                foreach (var attribute in element.Attributes().ToList())
                {
                    if (attribute.IsNamespaceDeclaration) attribute.Remove();
                    else if (attribute.Name.Namespace != XNamespace.None)
                    {
                        attribute.Remove();
                        if (element.Attribute(attribute.Name.LocalName) is null) element.SetAttributeValue(attribute.Name.LocalName, attribute.Value);
                    }
                }
            }
            return document;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}

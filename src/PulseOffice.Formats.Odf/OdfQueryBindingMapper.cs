using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfQueryBindingMapper
{
    private const string NamePrefix = "__PulseOffice_Query__";

    private readonly XNamespace _query = "urn:pulseoffice:query";

    private readonly OdfQualifiedRanges _ranges = new();

    public XNamespace Namespace => _query;

    public IEnumerable<XElement> Build(Workbook workbook)
    {
        foreach (var sheet in workbook.Sheets)
            foreach (var binding in sheet.QueryBindings.All.OrderBy(binding => binding.Id, StringComparer.Ordinal))
                yield return Range(sheet.Name, binding);
    }

    private XElement Range(string sheet, QueryBinding binding)
    {
        var target = binding.Range.IsSingleCell && binding.Range.Start == default ? CellRange.Single(binding.Anchor) : binding.Range;
        return new XElement(Namespaces.Table + "database-range",
            new XAttribute(Namespaces.Table + "name", NamePrefix + binding.Id),
            new XAttribute(Namespaces.Table + "target-range-address", _ranges.Write(sheet, target)),
            new XAttribute(Namespaces.Table + "contains-header", binding.IncludeHeaders ? "true" : "false"),
            new XAttribute(_query + "id", binding.Id),
            new XAttribute(_query + "connection", binding.ConnectionId),
            new XAttribute(_query + "anchor", binding.Anchor.ToA1()),
            new XAttribute(_query + "max-rows", binding.Query.MaxRows.ToString(CultureInfo.InvariantCulture)),
            new XAttribute(_query + "refresh", binding.Policy.ToString()),
            new XAttribute(_query + "interval", ((int)binding.RefreshInterval.TotalSeconds).ToString(CultureInfo.InvariantCulture)),
            new XAttribute(_query + "row-shift", binding.RowShift.ToString()),
            new XAttribute(_query + "preserve-formatting", binding.PreserveCellFormatting ? "true" : "false"),
            new XAttribute(_query + "rows", binding.LastRowCount.ToString(CultureInfo.InvariantCulture)),
            binding.LastRefresh is { } refreshed ? new XAttribute(_query + "refreshed", refreshed.ToString("O", CultureInfo.InvariantCulture)) : null,
            new XElement(Namespaces.Table + "database-source-sql",
                new XAttribute(Namespaces.Table + "database-name", binding.DisplayName),
                new XAttribute(Namespaces.Table + "sql-statement", binding.Query.Sql),
                new XAttribute(Namespaces.Table + "parse-sql-statement", "false")));
    }

    public bool TryRead(XElement element, Workbook workbook)
    {
        if ((string?)element.Attribute(_query + "id") is not { Length: > 0 } id) return false;
        if (_ranges.Read((string?)element.Attribute(Namespaces.Table + "target-range-address")) is not { } target) return true;
        var sheet = workbook.Sheets.FirstOrDefault(candidate => string.Equals(candidate.Name, target.Sheet, StringComparison.Ordinal));
        if (sheet is null) return true;

        var source = element.Element(Namespaces.Table + "database-source-sql");
        sheet.QueryBindings.AddOrUpdate(new QueryBinding
        {
            Id = id,
            DisplayName = (string?)source?.Attribute(Namespaces.Table + "database-name") ?? string.Empty,
            ConnectionId = (string?)element.Attribute(_query + "connection") ?? string.Empty,
            Query = new PulseOffice.Data.QueryDefinition
            {
                Sql = (string?)source?.Attribute(Namespaces.Table + "sql-statement") ?? string.Empty,
                MaxRows = Number(element, "max-rows") is > 0 and var rows ? rows : 100_000,
            },
            Anchor = CellAddress.TryParseA1((string?)element.Attribute(_query + "anchor"), out var anchor) ? anchor : target.Range.Start,
            Range = target.Range,
            IncludeHeaders = (string?)element.Attribute(Namespaces.Table + "contains-header") != "false",
            Policy = Enum.TryParse<RefreshPolicy>((string?)element.Attribute(_query + "refresh"), out var policy) ? policy : RefreshPolicy.Manual,
            RefreshInterval = Number(element, "interval") is > 0 and var seconds ? TimeSpan.FromSeconds(seconds) : TimeSpan.FromMinutes(10),
            RowShift = Enum.TryParse<RowShiftBehavior>((string?)element.Attribute(_query + "row-shift"), out var shift) ? shift : RowShiftBehavior.InsertDelete,
            PreserveCellFormatting = (string?)element.Attribute(_query + "preserve-formatting") != "false",
            LastRowCount = Number(element, "rows"),
            LastRefresh = DateTimeOffset.TryParse((string?)element.Attribute(_query + "refreshed"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var refreshed) ? refreshed : null,
        });
        return true;
    }

    private int Number(XElement element, string name) =>
        int.TryParse((string?)element.Attribute(_query + name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Max(0, value) : 0;
}

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed record OdfPivotExternalRead(PivotExternalSource Source, bool DroppedPassword, string Sheet = "", CellRange Range = default);

public sealed partial class OdfPivotSourceCodec
{
    private readonly OdfQualifiedRanges _ranges = new();
    private readonly XNamespace _pivot = "urn:pulseoffice:pivot";
    private readonly XName _sql = Namespaces.Table + "database-source-sql";
    private readonly XName _table = Namespaces.Table + "database-source-table";
    private readonly XName _query = Namespaces.Table + "database-source-query";
    private readonly XName _service = Namespaces.Table + "source-service";
    private readonly XName _range = Namespaces.Table + "source-cell-range";

    public bool IsExternal(XElement pilotTable)
    {
        ArgumentNullException.ThrowIfNull(pilotTable);
        if (pilotTable.Element(_sql) is not null || pilotTable.Element(_table) is not null
            || pilotTable.Element(_query) is not null || pilotTable.Element(_service) is not null) return true;
        return pilotTable.Element(_range)?.Attribute(Namespaces.Table + "cell-range-address")?.Value is { } address
            && ExternalAddress().IsMatch(address);
    }

    public XElement BuildSource(PivotDefinition pivot)
    {
        ArgumentNullException.ThrowIfNull(pivot);
        var source = pivot.ExternalSource ?? throw new ArgumentException("The pivot table has no external source.", nameof(pivot));
        var database = source.DatabaseName ?? string.Empty;
        var command = source.Command ?? string.Empty;
        return source.Kind switch
        {
            PivotExternalKind.Table => new XElement(_table,
                new XAttribute(Namespaces.Table + "database-name", database),
                new XAttribute(Namespaces.Table + "database-table-name", command)),
            PivotExternalKind.Query => new XElement(_query,
                new XAttribute(Namespaces.Table + "database-name", database),
                new XAttribute(Namespaces.Table + "query-name", command)),
            PivotExternalKind.Service => Service(source),
            PivotExternalKind.Workbook => new XElement(_range,
                new XAttribute(Namespaces.Table + "cell-range-address", "'" + database.Replace("'", "''", StringComparison.Ordinal) + "'#$" + _ranges.Write(pivot.SourceSheet, pivot.SourceRange))),
            _ => new XElement(_sql,
                new XAttribute(Namespaces.Table + "database-name", database),
                new XAttribute(Namespaces.Table + "sql-statement", command),
                source.ParseSql ? null : new XAttribute(Namespaces.Table + "parse-sql-statement", "false")),
        };
    }

    private XElement Service(PivotExternalSource source)
    {
        var element = new XElement(_service,
            new XAttribute(Namespaces.Table + "name", source.ServiceName ?? string.Empty),
            new XAttribute(Namespaces.Table + "source-name", source.ServiceSourceName ?? string.Empty),
            new XAttribute(Namespaces.Table + "object-name", source.ServiceObjectName ?? string.Empty));
        if (source.ServiceUserName is { Length: > 0 } user) element.Add(new XAttribute(Namespaces.Table + "user-name", user));
        return element;
    }

    public XElement BuildCache(PivotExternalSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var cache = new XElement(_pivot + "cache", new XAttribute(XNamespace.Xmlns + "pulsepivot", _pivot.NamespaceName));
        if (source.ConnectionId is { Length: > 0 } connection) cache.Add(new XAttribute(_pivot + "connection", connection));
        foreach (var header in source.Cache.Headers) cache.Add(new XElement(_pivot + "field", new XAttribute(_pivot + "name", header)));
        foreach (var record in source.Cache.Records)
            cache.Add(new XElement(_pivot + "record", record.Select(Value)));
        return cache;
    }

    private XElement Value(CellValue value)
    {
        var element = new XElement(_pivot + "value");
        switch (value.Kind)
        {
            case ValueKind.Empty: break;
            case ValueKind.Text: element.Add(new XAttribute(_pivot + "text", value.AsText())); break;
            case ValueKind.Boolean: element.Add(new XAttribute(_pivot + "boolean", value.AsBoolean() ? "true" : "false")); break;
            case ValueKind.Error: element.Add(new XAttribute(_pivot + "error", value.Error.ToString())); break;
            default: element.Add(new XAttribute(_pivot + "number", value.AsNumber().ToString("R", CultureInfo.InvariantCulture))); break;
        }
        return element;
    }

    public OdfPivotExternalRead? Read(XElement pilotTable)
    {
        ArgumentNullException.ThrowIfNull(pilotTable);
        var cache = pilotTable.Element(_pivot + "cache");
        var records = Cache(cache);
        var connection = cache?.Attribute(_pivot + "connection")?.Value;

        if (pilotTable.Element(_sql) is { } sql)
            return Found(new PivotExternalSource
            {
                Kind = PivotExternalKind.Sql,
                DatabaseName = sql.Attribute(Namespaces.Table + "database-name")?.Value,
                Command = sql.Attribute(Namespaces.Table + "sql-statement")?.Value,
                ParseSql = sql.Attribute(Namespaces.Table + "parse-sql-statement")?.Value != "false",
                ConnectionId = connection,
                Cache = records,
            });
        if (pilotTable.Element(_table) is { } table)
            return Found(Named(PivotExternalKind.Table, table, "database-table-name", connection, records));
        if (pilotTable.Element(_query) is { } query)
            return Found(Named(PivotExternalKind.Query, query, "query-name", connection, records));
        if (pilotTable.Element(_service) is { } service)
            return new OdfPivotExternalRead(new PivotExternalSource
            {
                Kind = PivotExternalKind.Service,
                ServiceName = service.Attribute(Namespaces.Table + "name")?.Value,
                ServiceSourceName = service.Attribute(Namespaces.Table + "source-name")?.Value,
                ServiceObjectName = service.Attribute(Namespaces.Table + "object-name")?.Value,
                ServiceUserName = service.Attribute(Namespaces.Table + "user-name")?.Value,
                ConnectionId = connection,
                Cache = records,
            }, service.Attribute(Namespaces.Table + "password") is { Value.Length: > 0 });
        if (pilotTable.Element(_range)?.Attribute(Namespaces.Table + "cell-range-address")?.Value is { } address
            && ExternalAddress().Match(address) is { Success: true } match
            && _ranges.Read(match.Groups["rest"].Value) is { } range)
            return new OdfPivotExternalRead(new PivotExternalSource
            {
                Kind = PivotExternalKind.Workbook,
                DatabaseName = match.Groups["url"].Value.Replace("''", "'", StringComparison.Ordinal),
                ConnectionId = connection,
                Cache = records,
            }, false, range.Sheet, range.Range);
        return null;
    }

    private PivotExternalSource Named(PivotExternalKind kind, XElement element, string commandAttribute, string? connection, PivotRecordCache records) => new()
    {
        Kind = kind,
        DatabaseName = element.Attribute(Namespaces.Table + "database-name")?.Value,
        Command = element.Attribute(Namespaces.Table + commandAttribute)?.Value,
        ConnectionId = connection,
        Cache = records,
    };

    private OdfPivotExternalRead Found(PivotExternalSource source) => new(source, false);

    private PivotRecordCache Cache(XElement? cache)
    {
        if (cache is null) return PivotRecordCache.Empty;
        var headers = cache.Elements(_pivot + "field").Select(field => field.Attribute(_pivot + "name")?.Value ?? string.Empty).ToList();
        var records = cache.Elements(_pivot + "record")
            .Select(record => (IReadOnlyList<CellValue>)[.. record.Elements(_pivot + "value").Select(ReadValue)])
            .ToList();
        return new PivotRecordCache { Headers = headers, Records = records };
    }

    private CellValue ReadValue(XElement value)
    {
        if (value.Attribute(_pivot + "text")?.Value is { } text) return CellValue.Text(text);
        if (value.Attribute(_pivot + "boolean")?.Value is { } flag) return CellValue.Boolean(flag == "true");
        if (value.Attribute(_pivot + "error")?.Value is { } error)
            return Enum.TryParse<CellError>(error, out var parsed) && Enum.IsDefined(parsed) && !int.TryParse(error, out _) ? CellValue.FromError(parsed) : CellValue.FromError(CellError.Value);
        if (double.TryParse(value.Attribute(_pivot + "number")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
            return CellValue.Number(number);
        return CellValue.Empty;
    }

    [GeneratedRegex(@"^'(?<url>(?:[^']|'')*)'#\$(?<rest>.+)$")]
    private static partial Regex ExternalAddress();
}

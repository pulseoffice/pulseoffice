using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Connection = DocumentFormat.OpenXml.Spreadsheet.Connection;

namespace PulseOffice.Formats.OpenXml;

internal sealed record XlsxExternalPivotRead(PivotExternalSource Source, string Sheet, CellRange Range);

internal sealed class XlsxPivotExternalSources
{
    private const uint SqlCommand = 2;
    private const uint TableCommand = 3;
    private const string ExtensionUri = "{5B0F3E1A-7C2D-4E8B-9F61-2A4D8C3E7B90}";
    private const string Namespace = "urn:pulseoffice:pivot";
    private const string Prefix = "pulsepivot";
    private const string ExternalLinkPath = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLinkPath";

    private readonly OoxmlTextEscape _escape = new();
    private readonly ExternalTargetUris _targets = new();
    private readonly DateTime _epoch = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);
    private readonly Dictionary<string, CellError> _errors = Enum.GetValues<CellError>()
        .Where(error => CellValue.FromError(error).ErrorText.Length > 0)
        .GroupBy(error => CellValue.FromError(error).ErrorText, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    public XlsxExternalPivotRead? Read(PivotTableCacheDefinitionPart cachePart, WorkbookPart workbookPart)
    {
        ArgumentNullException.ThrowIfNull(cachePart);
        ArgumentNullException.ThrowIfNull(workbookPart);

        if (cachePart.PivotCacheDefinition is not { CacheSource: { } cacheSource } definition) return null;
        var cache = Records(cachePart, definition);

        if (cacheSource.Type?.Value == SourceValues.Worksheet && cacheSource.WorksheetSource is { Id.Value: { Length: > 0 } id } worksheet)
        {
            var path = cachePart.ExternalRelationships.FirstOrDefault(relation => relation.Id == id)?.Uri.OriginalString;
            if (path is null || worksheet.Reference?.Value is not { } reference
                || !CellRange.TryParseA1(reference.Replace("$", string.Empty, StringComparison.Ordinal), out var range)) return null;
            return new XlsxExternalPivotRead(new PivotExternalSource
            {
                Kind = PivotExternalKind.Workbook,
                DatabaseName = path,
                Cache = cache,
            }, worksheet.Sheet?.Value ?? string.Empty, range);
        }

        if (cacheSource.Type?.Value != SourceValues.External || cacheSource.ConnectionId?.Value is not { } connectionId) return null;
        var connection = workbookPart.ConnectionsPart?.Connections?.Elements<Connection>().FirstOrDefault(candidate => candidate.Id?.Value == connectionId);
        if (connection is null || connection.OlapProperties is not null) return null;
        var ours = Ours(connection);
        var database = connection.DatabaseProperties;
        if (database is null && ours is null) return null;

        var kind = Enum.TryParse<PivotExternalKind>(Attribute(ours, "kind"), out var stored) ? stored
            : database?.CommandType?.Value == TableCommand ? PivotExternalKind.Table
            : PivotExternalKind.Sql;
        return new XlsxExternalPivotRead(new PivotExternalSource
        {
            Kind = kind,
            DatabaseName = ours is null ? connection.Name?.Value : Attribute(ours, "database"),
            Command = database?.Command?.Value is { Length: > 0 } command ? command : null,
            ParseSql = Attribute(ours, "parse-sql") != "false",
            ServiceName = Attribute(ours, "service"),
            ServiceSourceName = Attribute(ours, "service-source"),
            ServiceObjectName = Attribute(ours, "service-object"),
            ServiceUserName = Attribute(ours, "service-user"),
            ConnectionId = Attribute(ours, "connection"),
            Cache = cache,
        }, string.Empty, default);
    }

    public CacheSource Source(PivotDefinition pivot, PivotTableCacheDefinitionPart cachePart, WorkbookPart workbookPart)
    {
        ArgumentNullException.ThrowIfNull(pivot);
        ArgumentNullException.ThrowIfNull(cachePart);
        ArgumentNullException.ThrowIfNull(workbookPart);
        var source = pivot.ExternalSource ?? throw new ArgumentException("The pivot table has no external source.", nameof(pivot));

        if (source.Kind == PivotExternalKind.Workbook)
        {
            var relation = cachePart.AddExternalRelationship(ExternalLinkPath, _targets.Of(source.DatabaseName ?? string.Empty));
            return new CacheSource(new WorksheetSource
            {
                Reference = pivot.SourceRange.ToString(),
                Sheet = pivot.SourceSheet,
                Id = relation.Id,
            }) { Type = SourceValues.Worksheet };
        }

        var connections = workbookPart.ConnectionsPart ?? workbookPart.AddNewPart<ConnectionsPart>();
        connections.Connections ??= new Connections();
        var id = connections.Connections.Elements<Connection>().Select(existing => existing.Id?.Value ?? 0).DefaultIfEmpty(0u).Max() + 1;
        connections.Connections.AppendChild(Connection(id, pivot, source));
        connections.Connections.Save();
        return new CacheSource { Type = SourceValues.External, ConnectionId = id };
    }

    private Connection Connection(uint id, PivotDefinition pivot, PivotExternalSource source)
    {
        var marker = new OpenXmlUnknownElement(Prefix, "source", Namespace);
        foreach (var (name, value) in new (string, string?)[]
        {
            ("kind", source.Kind.ToString()),
            ("database", source.DatabaseName),
            ("parse-sql", source.ParseSql ? null : "false"),
            ("service", source.ServiceName),
            ("service-source", source.ServiceSourceName),
            ("service-object", source.ServiceObjectName),
            ("service-user", source.ServiceUserName),
            ("connection", source.ConnectionId),
        })
            if (value is not null) marker.SetAttribute(new OpenXmlAttribute(name, string.Empty, value));

        return new Connection(
            new DatabaseProperties
            {
                Connection = source.ConnectionId is { Length: > 0 } known ? $"DSN={known}" : $"DSN={source.DatabaseName ?? source.ServiceSourceName ?? string.Empty}",
                Command = source.Command ?? string.Empty,
                CommandType = source.Kind is PivotExternalKind.Table or PivotExternalKind.Query ? TableCommand : SqlCommand,
            },
            new ConnectionExtensionList(new ConnectionExtension(marker) { Uri = ExtensionUri }))
        {
            Id = id,
            Name = Unique(source.DatabaseName is { Length: > 0 } database ? database : pivot.Name, id),
            Type = 1u,
            RefreshedVersion = 6,
            SaveData = true,
        };
    }

    private string Unique(string name, uint id) => name + " (" + id.ToString(CultureInfo.InvariantCulture) + ")";

    private PivotRecordCache Records(PivotTableCacheDefinitionPart cachePart, PivotCacheDefinition definition)
    {
        var fields = definition.CacheFields?.Elements<CacheField>().Where(field => field.DatabaseField?.Value != false).ToList() ?? [];
        if (fields.Count == 0 || cachePart.PivotTableCacheRecordsPart is null) return PivotRecordCache.Empty;
        var shared = fields.Select(field => field.SharedItems?.ChildElements.Select(Item).ToList() ?? []).ToList();
        var records = new List<IReadOnlyList<CellValue>>();
        foreach (var record in cachePart.PivotTableCacheRecordsPart?.PivotCacheRecords?.Elements<PivotCacheRecord>() ?? [])
        {
            var values = new List<CellValue>(Math.Min(record.ChildElements.Count, fields.Count));
            foreach (var item in record.ChildElements)
            {
                if (values.Count >= fields.Count) break;
                var column = values.Count;
                values.Add(item is FieldItem index
                    ? index.Val?.Value is { } at && at < shared[column].Count ? shared[column][(int)at] : CellValue.Empty
                    : Item(item));
            }
            records.Add(values);
        }
        return new PivotRecordCache { Headers = [.. fields.Select(field => _escape.Decode(field.Name?.Value ?? string.Empty))], Records = records };
    }

    private CellValue Item(OpenXmlElement item) => item switch
    {
        NumberItem number when number.Val?.Value is { } value && double.IsFinite(value) => CellValue.Number(value),
        BooleanItem boolean => CellValue.Boolean(boolean.Val?.Value == true),
        StringItem text => CellValue.Text(_escape.Decode(text.Val?.Value ?? string.Empty)),
        ErrorItem error => CellValue.FromError(_errors.TryGetValue(error.Val?.Value ?? string.Empty, out var known) ? known : CellError.Value),
        DateTimeItem date when date.Val?.Value is { } moment => CellValue.Number((moment - _epoch).TotalDays),
        _ => CellValue.Empty,
    };

    private OpenXmlElement? Ours(Connection connection) =>
        connection.GetFirstChild<ConnectionExtensionList>()?.Elements<ConnectionExtension>()
            .Where(extension => extension.Uri?.Value == ExtensionUri)
            .SelectMany(extension => extension.ChildElements)
            .FirstOrDefault(element => element.NamespaceUri == Namespace);

    private string? Attribute(OpenXmlElement? element, string name) =>
        element?.GetAttributes().Where(attribute => attribute.LocalName == name).Select(attribute => attribute.Value).FirstOrDefault();
}

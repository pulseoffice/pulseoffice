using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Book = PulseOffice.Sheets.Workbook;
using Sheet = PulseOffice.Sheets.Worksheet;
using DefinedName = DocumentFormat.OpenXml.Spreadsheet.DefinedName;
using QueryTable = DocumentFormat.OpenXml.Spreadsheet.QueryTable;
using Connection = DocumentFormat.OpenXml.Spreadsheet.Connection;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxQueryTableMapper
{
    private const string NamePrefix = "ExternalData_";
    private const string ExtensionUri = "{8C1F7B54-5E2B-4C55-9A57-31D2A0E6C7B1}";
    private const string Namespace = "urn:pulseoffice:query";
    private const string Prefix = "pulseoffice";

    private readonly XlsxQueryTableNames _names = new();

    public IEnumerable<DefinedName> DefinedNames(Book workbook)
    {
        var names = Names(workbook);
        for (var index = 0; index < workbook.Sheets.Count; index++)
        {
            var sheet = workbook.Sheets[index];
            foreach (var binding in Ordered(sheet))
                yield return new DefinedName
                {
                    Name = names[binding],
                    LocalSheetId = (uint)index,
                    Text = $"'{sheet.Name.Replace("'", "''", StringComparison.Ordinal)}'!{Absolute(Target(binding))}",
                };
        }
    }

    private Dictionary<QueryBinding, string> Names(Book workbook)
    {
        var taken = workbook.DatabaseRanges.All.Select(range => range.Name)
            .Concat(workbook.DefinedNames.Select(name => name.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<QueryBinding, string>(ReferenceEqualityComparer.Instance);
        var ordinal = 0;
        foreach (var sheet in workbook.Sheets)
            foreach (var binding in Ordered(sheet))
            {
                var name = NamePrefix + ++ordinal;
                while (taken.Contains(name)) name = NamePrefix + ++ordinal;
                names[binding] = name;
            }
        return names;
    }

    public void Write(WorkbookPart workbookPart, Book workbook, IReadOnlyDictionary<Sheet, WorksheetPart> parts)
    {
        var connections = new Connections();
        var names = Names(workbook);
        var ordinal = 0u;
        foreach (var sheet in workbook.Sheets)
            foreach (var binding in Ordered(sheet))
            {
                ordinal++;
                connections.AppendChild(Connection(ordinal, binding));
                var part = parts[sheet].AddNewPart<QueryTablePart>();
                part.QueryTable = QueryTable(ordinal, names[binding], binding);
                part.QueryTable.Save();
            }

        if (ordinal == 0) return;
        var connectionsPart = workbookPart.AddNewPart<ConnectionsPart>();
        connectionsPart.Connections = connections;
        connectionsPart.Connections.Save();
    }

    public void Read(WorkbookPart workbookPart, WorksheetPart part, Sheet sheet, int sheetIndex)
    {
        var connections = workbookPart.ConnectionsPart?.Connections?.Elements<Connection>().ToList() ?? [];
        foreach (var queryPart in part.QueryTableParts)
        {
            if (queryPart.QueryTable is not { } table) continue;
            var connection = connections.FirstOrDefault(candidate => candidate.Id?.Value == table.ConnectionId?.Value);
            if (connection?.DatabaseProperties is not { } database) continue;
            var ours = Ours(table);
            var range = Range(Attribute(ours, "range")) ?? _names.Range(workbookPart, sheetIndex, table.Name?.Value);
            if (range is not { } target) continue;

            var id = Attribute(ours, "id") is { Length: > 0 } own ? own : $"query{sheet.QueryBindings.Count + 1}";
            sheet.QueryBindings.AddOrUpdate(new QueryBinding
            {
                Id = id,
                DisplayName = connection.Name?.Value ?? id,
                ConnectionId = Attribute(ours, "connection") ?? connection.Name?.Value ?? string.Empty,
                Query = new PulseOffice.Data.QueryDefinition
                {
                    Sql = database.Command?.Value ?? string.Empty,
                    MaxRows = Number(ours, "max-rows") is > 0 and var rows ? rows : 100_000,
                },
                Anchor = CellAddress.TryParseA1(Attribute(ours, "anchor"), out var anchor) ? anchor : target.Start,
                Range = target,
                IncludeHeaders = table.Headers?.Value != false,
                Policy = Enum.TryParse<RefreshPolicy>(Attribute(ours, "refresh"), out var policy) ? policy
                    : connection.RefreshOnLoad?.Value == true ? RefreshPolicy.OnOpen
                    : connection.Interval?.Value is > 0 ? RefreshPolicy.Periodic
                    : RefreshPolicy.Manual,
                RefreshInterval = Number(ours, "interval") is > 0 and var seconds ? TimeSpan.FromSeconds(seconds)
                    : connection.Interval?.Value is > 0 and var minutes ? TimeSpan.FromMinutes(minutes)
                    : TimeSpan.FromMinutes(10),
                RowShift = Enum.TryParse<RowShiftBehavior>(Attribute(ours, "row-shift"), out var shift) ? shift : Shift(table.GrowShrinkType?.Value),
                PreserveCellFormatting = table.PreserveFormatting?.Value != false,
                LastRowCount = Number(ours, "rows"),
                LastRefresh = DateTimeOffset.TryParse(Attribute(ours, "refreshed"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var refreshed) ? refreshed : null,
            });
        }
    }

    private IEnumerable<QueryBinding> Ordered(Sheet sheet) => sheet.QueryBindings.All.OrderBy(binding => binding.Id, StringComparer.Ordinal);

    private Connection Connection(uint id, QueryBinding binding) => new(new DatabaseProperties
    {
        Connection = $"DSN={binding.ConnectionId}",
        Command = binding.Query.Sql,
        CommandType = 2u,
    })
    {
        Id = id,
        Name = binding.DisplayName.Length > 0 ? binding.DisplayName : binding.Id,
        Type = 1u,
        RefreshedVersion = 8,
        SaveData = true,
        RefreshOnLoad = binding.Policy == RefreshPolicy.OnOpen ? true : null,
        Interval = binding.Policy == RefreshPolicy.Periodic ? (uint)Math.Max(1, Math.Round(binding.RefreshInterval.TotalMinutes)) : null,
    };

    private QueryTable QueryTable(uint id, string tableName, QueryBinding binding)
    {
        var marker = new OpenXmlUnknownElement(Prefix, "query", Namespace);
        foreach (var (name, value) in new (string, string?)[]
        {
            ("id", binding.Id),
            ("connection", binding.ConnectionId),
            ("anchor", binding.Anchor.ToA1()),
            ("range", Target(binding).ToString()),
            ("max-rows", binding.Query.MaxRows.ToString(CultureInfo.InvariantCulture)),
            ("refresh", binding.Policy.ToString()),
            ("interval", ((int)binding.RefreshInterval.TotalSeconds).ToString(CultureInfo.InvariantCulture)),
            ("row-shift", binding.RowShift.ToString()),
            ("rows", binding.LastRowCount.ToString(CultureInfo.InvariantCulture)),
            ("refreshed", binding.LastRefresh?.ToString("O", CultureInfo.InvariantCulture)),
        })
            if (value is not null) marker.SetAttribute(new OpenXmlAttribute(name, string.Empty, value));

        return new QueryTable(new QueryTableExtensionList(new QueryTableExtension(marker) { Uri = ExtensionUri }))
        {
            Name = tableName,
            ConnectionId = id,
            Headers = binding.IncludeHeaders,
            PreserveFormatting = binding.PreserveCellFormatting,
            RefreshOnLoad = binding.Policy == RefreshPolicy.OnOpen ? true : null,
            AdjustColumnWidth = false,
            GrowShrinkType = binding.RowShift switch
            {
                RowShiftBehavior.Overwrite => GrowShrinkValues.OverwriteClear,
                RowShiftBehavior.Clip => GrowShrinkValues.InsertClear,
                _ => GrowShrinkValues.InsertDelete,
            },
        };
    }

    private RowShiftBehavior Shift(GrowShrinkValues? value) =>
        value == GrowShrinkValues.OverwriteClear ? RowShiftBehavior.Overwrite
        : value == GrowShrinkValues.InsertClear ? RowShiftBehavior.Clip
        : RowShiftBehavior.InsertDelete;

    private CellRange Target(QueryBinding binding) =>
        binding.Range.IsSingleCell && binding.Range.Start == default ? CellRange.Single(binding.Anchor) : binding.Range;

    private string Absolute(CellRange range) =>
        $"{range.Start.ToA1(true, true)}:{range.End.ToA1(true, true)}";

    private OpenXmlElement? Ours(QueryTable table) =>
        table.GetFirstChild<QueryTableExtensionList>()?.Elements<QueryTableExtension>()
            .Where(extension => extension.Uri?.Value == ExtensionUri)
            .SelectMany(extension => extension.ChildElements)
            .FirstOrDefault(element => element.NamespaceUri == Namespace);

    private string? Attribute(OpenXmlElement? element, string name) =>
        element?.GetAttributes().Where(attribute => attribute.LocalName == name).Select(attribute => attribute.Value).FirstOrDefault();

    private int Number(OpenXmlElement? element, string name) =>
        int.TryParse(Attribute(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private CellRange? Range(string? text) => text is not null && CellRange.TryParseA1(text, out var range) ? range : null;
}

internal sealed class XlsxQueryTableNames
{
    public bool Claims(WorkbookPart workbookPart, uint? sheetIndex, string? name) =>
        sheetIndex is { } index && index <= int.MaxValue && name is { Length: > 0 } && QueryNames(workbookPart, (int)index).Contains(name);

    public CellRange? Range(WorkbookPart workbookPart, int sheetIndex, string? name)
    {
        var defined = workbookPart.Workbook?.DefinedNames?.Elements<DefinedName>()
            .FirstOrDefault(candidate => candidate.LocalSheetId?.Value == (uint)sheetIndex && candidate.Name?.Value == name);
        if (defined?.Text is not { } text) return null;
        var reference = text[(text.LastIndexOf('!') + 1)..].Replace("$", string.Empty, StringComparison.Ordinal);
        return CellRange.TryParseA1(reference, out var range) ? range : null;
    }

    private HashSet<string> QueryNames(WorkbookPart workbookPart, int sheetIndex)
    {
        var sheets = workbookPart.Workbook?.Sheets?.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().ToList() ?? [];
        if (sheetIndex < 0 || sheetIndex >= sheets.Count || sheets[sheetIndex].Id?.Value is not { } id || !workbookPart.TryGetPartById(id, out var part) || part is not WorksheetPart worksheet)
            return [];
        return [.. worksheet.QueryTableParts.Select(query => query.QueryTable?.Name?.Value).OfType<string>()];
    }
}

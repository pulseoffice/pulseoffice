using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Formats.OpenXml;

internal sealed record XlsxPivotSource(string Sheet, CellRange Range);

internal sealed class XlsxPivotSourceResolver
{
    private readonly Dictionary<string, XlsxPivotSource> _named = new(StringComparer.OrdinalIgnoreCase);

    public XlsxPivotSourceResolver(WorkbookPart workbookPart)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);

        foreach (var sheet in workbookPart.Workbook?.Sheets?.Elements<Sheet>() ?? [])
        {
            if (sheet.Id?.Value is not { } id || sheet.Name?.Value is not { } sheetName) continue;
            if (!workbookPart.TryGetPartById(id, out var found) || found is not WorksheetPart part) continue;
            foreach (var table in part.TableDefinitionParts.Select(table => table.Table).OfType<Table>())
                if (table.Reference?.Value is { } reference && CellRange.TryParseA1(reference, out var range))
                    foreach (var name in new[] { table.DisplayName?.Value, table.Name?.Value }.OfType<string>())
                        _named.TryAdd(name, new XlsxPivotSource(sheetName, range));
        }

        foreach (var defined in workbookPart.Workbook?.DefinedNames?.Elements<DefinedName>() ?? [])
            if (defined.Name?.Value is { } name && defined.LocalSheetId is null && Area(defined.Text) is { } area)
                _named.TryAdd(name, area);
    }

    public bool IsExternal(CacheSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Type?.Value != SourceValues.Worksheet || source.WorksheetSource?.Id?.Value is { Length: > 0 };
    }

    public XlsxPivotSource? Resolve(WorksheetSource source, string pivotSheet)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Reference?.Value is { } reference && CellRange.TryParseA1(reference.Replace("$", string.Empty, StringComparison.Ordinal), out var range))
            return new XlsxPivotSource(source.Sheet?.Value ?? pivotSheet, range);
        return source.Name?.Value is { } name && _named.TryGetValue(name, out var named) ? named : null;
    }

    private XlsxPivotSource? Area(string? text)
    {
        if (text is null) return null;
        var separator = text.LastIndexOf('!');
        if (separator <= 0) return null;
        var sheet = text[..separator].Trim('\'').Replace("''", "'", StringComparison.Ordinal);
        return CellRange.TryParseA1(text[(separator + 1)..].Replace("$", string.Empty, StringComparison.Ordinal), out var range)
            ? new XlsxPivotSource(sheet, range)
            : null;
    }
}

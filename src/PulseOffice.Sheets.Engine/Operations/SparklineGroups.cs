using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record SparklineLayoutRequest(
    int HostSheet, SheetRangeReference Data, CellRange Destination, SheetRangeReference? Dates, SparklineGroup Options);

public sealed record SparklineGroupText(string Data, string Destination, string Dates);

public interface ISparklineGroups
{
    SparklineGroup? Lay(Workbook workbook, SparklineLayoutRequest request);

    SparklineGroupText Describe(Workbook workbook, int hostSheet, SparklineGroup group);

    IReadOnlyList<SparklineGroup>? Grouped(SparklineCollection sparklines, CellRange selection, CellAddress active, string id);

    IReadOnlyList<SparklineGroup>? Ungrouped(SparklineCollection sparklines, CellRange selection);
}

public sealed class SparklineGroups(ISheetRangeReferences references) : ISparklineGroups
{
    public SparklineGroup? Lay(Workbook workbook, SparklineLayoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Data.SheetIndex < 0 || request.Data.SheetIndex >= workbook.Sheets.Count) return null;

        var sourceSheet = workbook.Sheets[request.Data.SheetIndex];
        var requested = request.Data.Range;
        if (sourceSheet.UsedRange() is not { } used
            || used.End.Row < requested.Start.Row || used.End.Column < requested.Start.Column)
            return null;

        var data = new CellRange(requested.Start,
            new CellAddress(Math.Min(requested.End.Row, used.End.Row), Math.Min(requested.End.Column, used.End.Column)));
        var target = request.Destination;
        var byRow = data.RowCount >= data.ColumnCount || target.ColumnCount == 1;
        var destinations = target.ColumnCount == 1 ? target.RowCount : target.ColumnCount;
        var slots = byRow ? data.RowCount : data.ColumnCount;
        if (destinations > 1) slots = Math.Min(slots, destinations);
        var sheetName = request.Data.SheetIndex == request.HostSheet ? null : sourceSheet.Name;

        var lines = new List<Sparkline>();
        for (var index = 0; index < slots; index++)
        {
            var source = byRow
                ? new CellRange(new CellAddress(data.Start.Row + index, data.Start.Column), new CellAddress(data.Start.Row + index, data.End.Column))
                : new CellRange(new CellAddress(data.Start.Row, data.Start.Column + index), new CellAddress(data.End.Row, data.Start.Column + index));
            var cell = target.ColumnCount == 1
                ? new CellAddress(target.Start.Row + index, target.Start.Column)
                : new CellAddress(target.Start.Row, target.Start.Column + index);
            if (cell.IsValid) lines.Add(new Sparkline(cell, source, sheetName));
        }

        if (lines.Count == 0) return null;

        var dates = request.Dates;
        return request.Options with
        {
            Sparklines = lines,
            DateRange = dates?.Range,
            DateSheet = dates is null || dates.SheetIndex == request.HostSheet || dates.SheetIndex >= workbook.Sheets.Count
                ? null
                : workbook.Sheets[dates.SheetIndex].Name,
        };
    }

    public SparklineGroupText Describe(Workbook workbook, int hostSheet, SparklineGroup group)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(group);

        var sources = group.Sparklines;
        var data = sources.Count == 0 ? string.Empty
            : Text(workbook, hostSheet, sources[0].SourceSheet, Bounds(sources.Select(line => line.Source)));
        var cells = sources.Count == 0 ? string.Empty
            : Bounds(sources.Select(line => new CellRange(line.Cell, line.Cell))).ToString();
        var dates = group.DateRange is { } range ? Text(workbook, hostSheet, group.DateSheet, range) : string.Empty;
        return new SparklineGroupText(data, cells, dates);
    }

    public IReadOnlyList<SparklineGroup>? Grouped(SparklineCollection sparklines, CellRange selection, CellAddress active, string id)
    {
        ArgumentNullException.ThrowIfNull(sparklines);

        var touched = sparklines.Groups.Where(group => group.Sparklines.Any(line => selection.Contains(line.Cell))).ToList();
        if (touched.Count < 2) return null;

        var lead = touched.FirstOrDefault(group => group.Contains(active)) ?? touched[0];
        var merged = lead with { Id = id, Sparklines = [.. touched.SelectMany(group => group.Sparklines)] };
        return [.. sparklines.Groups.Where(group => !touched.Contains(group)), merged];
    }

    public IReadOnlyList<SparklineGroup>? Ungrouped(SparklineCollection sparklines, CellRange selection)
    {
        ArgumentNullException.ThrowIfNull(sparklines);

        var touched = sparklines.Groups.Where(group => group.Sparklines.Count > 1 && group.Sparklines.Any(line => selection.Contains(line.Cell))).ToList();
        if (touched.Count == 0) return null;

        var split = touched.SelectMany(group => group.Sparklines.Select((line, index) => group with { Id = index == 0 ? group.Id : Guid.NewGuid().ToString("N"), Sparklines = [line] }));
        return [.. sparklines.Groups.Where(group => !touched.Contains(group)), .. split];
    }

    private string Text(Workbook workbook, int hostSheet, string? sheet, CellRange range)
    {
        var index = sheet is null ? hostSheet : IndexOf(workbook, sheet);
        return index < 0 ? range.ToString() : references.Qualify(workbook, index, range.ToString(), hostSheet);
    }

    private static int IndexOf(Workbook workbook, string name)
    {
        for (var index = 0; index < workbook.Sheets.Count; index++)
            if (string.Equals(workbook.Sheets[index].Name, name, StringComparison.OrdinalIgnoreCase)) return index;
        return -1;
    }

    private static CellRange Bounds(IEnumerable<CellRange> ranges)
    {
        var list = ranges.ToList();
        return new CellRange(
            new CellAddress(list.Min(range => range.Start.Row), list.Min(range => range.Start.Column)),
            new CellAddress(list.Max(range => range.End.Row), list.Max(range => range.End.Column)));
    }
}

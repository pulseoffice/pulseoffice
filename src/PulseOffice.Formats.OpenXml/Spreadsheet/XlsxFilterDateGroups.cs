using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Sheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxFilterDateGroups
{
    private const double FirstSerial = -657435;
    private const double LastSerial = 2958465;

    public IEnumerable<string> Matching(Filters filters, Sheet? sheet, CellRange range, int columnOffset)
    {
        var groups = filters.Elements<DateGroupItem>().ToList();
        if (groups.Count == 0 || sheet is null) yield break;

        var column = range.Start.Column + columnOffset;
        if (column > range.End.Column) yield break;

        for (var row = range.Start.Row + 1; row <= range.End.Row; row++)
        {
            var value = sheet[new CellAddress(row, column)].Value;
            if (value.Kind != ValueKind.Number || value.AsNumber() is not (> FirstSerial and < LastSerial and var serial)) continue;
            var moment = DateTime.FromOADate(serial);
            if (groups.Any(group => Matches(group, moment))) yield return value.ToString();
        }
    }

    public DateGroupItem Item(DateTime moment)
    {
        var timed = moment.TimeOfDay != TimeSpan.Zero;
        return new DateGroupItem
        {
            Year = (ushort)moment.Year,
            Month = (ushort)moment.Month,
            Day = (ushort)moment.Day,
            Hour = timed ? (ushort)moment.Hour : null,
            Minute = timed ? (ushort)moment.Minute : null,
            Second = timed ? (ushort)moment.Second : null,
            DateTimeGrouping = timed ? DateTimeGroupingValues.Second : DateTimeGroupingValues.Day,
        };
    }

    private bool Matches(DateGroupItem group, DateTime moment)
    {
        var grouping = group.DateTimeGrouping?.Value ?? DateTimeGroupingValues.Year;
        var depth = Depth(grouping);
        return Same(group.Year?.Value, moment.Year)
            && (depth < 1 || Same(group.Month?.Value, moment.Month))
            && (depth < 2 || Same(group.Day?.Value, moment.Day))
            && (depth < 3 || Same(group.Hour?.Value, moment.Hour))
            && (depth < 4 || Same(group.Minute?.Value, moment.Minute))
            && (depth < 5 || Same(group.Second?.Value, moment.Second));
    }

    private int Depth(DateTimeGroupingValues grouping) =>
        grouping == DateTimeGroupingValues.Second ? 5
        : grouping == DateTimeGroupingValues.Minute ? 4
        : grouping == DateTimeGroupingValues.Hour ? 3
        : grouping == DateTimeGroupingValues.Day ? 2
        : grouping == DateTimeGroupingValues.Month ? 1
        : 0;

    private bool Same(ushort? stored, int actual) => stored is { } value && value == actual;
}

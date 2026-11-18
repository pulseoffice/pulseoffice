using System.Globalization;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Sheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxDynamicFilterReader
{
    private const double FirstSerial = -657435;
    private const double LastSerial = 2958465;

    private static readonly (DynamicFilterValues Type, int[] Months)[] Periods =
    [
        (DynamicFilterValues.January, [1]), (DynamicFilterValues.February, [2]), (DynamicFilterValues.March, [3]),
        (DynamicFilterValues.April, [4]), (DynamicFilterValues.May, [5]), (DynamicFilterValues.June, [6]),
        (DynamicFilterValues.July, [7]), (DynamicFilterValues.August, [8]), (DynamicFilterValues.September, [9]),
        (DynamicFilterValues.October, [10]), (DynamicFilterValues.November, [11]), (DynamicFilterValues.December, [12]),
        (DynamicFilterValues.Quarter1, [1, 2, 3]), (DynamicFilterValues.Quarter2, [4, 5, 6]),
        (DynamicFilterValues.Quarter3, [7, 8, 9]), (DynamicFilterValues.Quarter4, [10, 11, 12]),
    ];

    public ColumnFilter Apply(ColumnFilter filter, DynamicFilter dynamic, Sheet? sheet, CellRange range)
    {
        var type = dynamic.Type?.Value;
        if (Periods.FirstOrDefault(period => period.Type == type).Months is { } months)
            return filter with { AllowedValues = Matching(sheet, range, filter.ColumnOffset, months), IncludeBlanks = false };
        if (dynamic.Val?.Value is not { } value || !double.IsFinite(value)) return filter;
        if (type == DynamicFilterValues.AboveAverage) return filter with { Criteria = ">" + Text(value) };
        if (type == DynamicFilterValues.BelowAverage) return filter with { Criteria = "<" + Text(value) };
        if (dynamic.MaxVal?.Value is not { } max || !double.IsFinite(max)) return filter;
        return filter with { Criteria = ">=" + Text(value), SecondCriteria = "<" + Text(max), Join = CriteriaJoin.And };
    }

    private HashSet<string> Matching(Sheet? sheet, CellRange range, int columnOffset, int[] months)
    {
        var allowed = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var column = range.Start.Column + columnOffset;
        if (sheet is null || column > range.End.Column) return allowed;
        for (var row = range.Start.Row + 1; row <= range.End.Row; row++)
        {
            var cell = sheet[new CellAddress(row, column)].Value;
            if (cell.Kind != ValueKind.Number || cell.AsNumber() is not (> FirstSerial and < LastSerial and var serial)) continue;
            if (months.Contains(DateTime.FromOADate(serial).Month)) allowed.Add(cell.ToString());
        }
        return allowed;
    }

    private string Text(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}

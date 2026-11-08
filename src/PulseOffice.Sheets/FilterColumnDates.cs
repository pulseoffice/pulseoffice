using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets;

public interface IFilterColumnDates
{
    bool TryRead(Worksheet sheet, CellAddress address, Styling.StyleCatalog styles, Formatting.NumberFormatCatalog formats, out DateTime moment);

    IReadOnlyDictionary<string, DateTime> Read(Workbook workbook, Worksheet sheet, CellRange range, int columnOffset, bool hasHeader);

    string IsoText(DateTime moment);

    IReadOnlySet<string> Resolve(IEnumerable<string> stored, IReadOnlyDictionary<string, DateTime> dates);
}

public sealed class FilterColumnDates : IFilterColumnDates
{
    private const double FirstSerial = -657435;
    private const double LastSerial = 2958465;
    private const string DateOnly = "yyyy-MM-dd";
    private const string DateAndTime = "yyyy-MM-dd HH:mm:ss";

    public bool TryRead(Worksheet sheet, CellAddress address, Styling.StyleCatalog styles, Formatting.NumberFormatCatalog formats, out DateTime moment)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(formats);
        moment = default;
        var value = sheet[address].Value;
        if (value.Kind != ValueKind.Number || value.AsNumber() is not (> FirstSerial and < LastSerial and var serial)) return false;
        if (formats.Get(styles.Get(sheet.StyleIdAt(address)).NumberFormatId).Sections is not [{ ValueKind: Formatting.FormatValueKind.DateTime }, ..]) return false;
        moment = DateTime.FromOADate(serial);
        return true;
    }

    public IReadOnlyDictionary<string, DateTime> Read(Workbook workbook, Worksheet sheet, CellRange range, int columnOffset, bool hasHeader)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        var column = range.Start.Column + columnOffset;
        if (columnOffset < 0 || column > range.End.Column) return dates;
        for (var row = range.Start.Row + (hasHeader ? 1 : 0); row <= range.End.Row; row++)
        {
            var address = new CellAddress(row, column);
            if (TryRead(sheet, address, workbook.Styles, workbook.NumberFormats, out var moment))
                dates[sheet[address].Value.ToString()] = moment;
        }
        return dates;
    }

    public string IsoText(DateTime moment) =>
        moment.ToString(moment.TimeOfDay == TimeSpan.Zero ? DateOnly : DateAndTime, CultureInfo.InvariantCulture);

    public IReadOnlySet<string> Resolve(IEnumerable<string> stored, IReadOnlyDictionary<string, DateTime> dates)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(dates);
        var resolved = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var item in stored)
        {
            var matches = dates.Count == 0 ? [] : Matching(item, dates).ToList();
            if (matches.Count == 0) resolved.Add(item);
            else resolved.UnionWith(matches);
        }
        return resolved;
    }

    private IEnumerable<string> Matching(string item, IReadOnlyDictionary<string, DateTime> dates)
    {
        if (DateTime.TryParseExact(item, DateAndTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment))
            return dates.Where(pair => Math.Abs((pair.Value - moment).TotalSeconds) < 0.5).Select(pair => pair.Key);
        if (DateTime.TryParseExact(item, DateOnly, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return dates.Where(pair => pair.Value.Date == day).Select(pair => pair.Key);
        return [];
    }
}

using System.Text.RegularExpressions;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxCalendarCells(Workbook workbook)
{
    private readonly Regex _literals = new("\"[^\"]*\"|\\[[^\\]]*\\]|\\\\.", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private readonly Regex _calendarPart = new("[dDyY]|[mM]{3,}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private readonly Dictionary<int, bool> _calendarFormats = [];

    public IReadOnlyList<(Worksheet Sheet, CellAddress Address, Cell Cell)> ConstantDates() =>
        [.. workbook.Sheets.SelectMany(sheet => sheet.NonEmptyCells()
            .Where(entry => !entry.Value.HasFormula && entry.Value.Value.Kind == ValueKind.Number
                && HasCalendarFormat(workbook.Styles.Get(sheet.StyleIdAt(entry.Key)).NumberFormatId))
            .Select(entry => (sheet, entry.Key, entry.Value)))];

    public bool HasCalendarFormat(int formatId)
    {
        if (_calendarFormats.TryGetValue(formatId, out var known)) return known;
        var code = _literals.Replace(workbook.NumberFormats.CodeOf(formatId), string.Empty);
        var calendar = _calendarPart.IsMatch(code) && !code.Contains("General", StringComparison.OrdinalIgnoreCase);
        _calendarFormats[formatId] = calendar;
        return calendar;
    }
}

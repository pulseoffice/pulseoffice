using System.Collections.Concurrent;
using System.Globalization;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Formatting;

public sealed class LocaleNumberFormatter(ISpreadsheetLocale locale, Func<double, DateTime> serialToDate) : INumberFormatter
{
    private readonly ConcurrentDictionary<CultureInfo, NumberFormatter> _formatters = new();

    public FormattedCell Format(CellValue value, NumberFormat format, double columnWidthInCharacters = 12) =>
        _formatters.GetOrAdd(locale.Culture, culture => new NumberFormatter(culture, serialToDate))
            .Format(value, format, columnWidthInCharacters);
}

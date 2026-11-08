using System.Collections.Concurrent;
using System.Globalization;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public interface IFormatCodeRenderer
{
    string Render(CellValue value, string code);
}

public sealed class FormatCodeRenderer(ISpreadsheetLocale locale, IDateSystem dates, INumberFormatParser parser) : IFormatCodeRenderer
{
    private const double UnlimitedWidth = 1_000_000;

    private readonly ConcurrentDictionary<CultureInfo, INumberFormatter> _formatters = new();
    private readonly ConcurrentDictionary<string, NumberFormat> _formats = new(StringComparer.Ordinal);

    public string Render(CellValue value, string code)
    {
        var format = _formats.GetOrAdd(code, parser.Parse);
        var formatter = _formatters.GetOrAdd(locale.Culture, culture => new NumberFormatter(culture, dates.FromSerial));
        return formatter.Format(value, format, UnlimitedWidth).Text;
    }
}

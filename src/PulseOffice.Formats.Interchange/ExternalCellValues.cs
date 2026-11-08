using System.Globalization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Formats.Interchange;

public sealed class ExternalCellValues(ICsvNumberParser numbers, ISpreadsheetLocale locale) : IExternalCellValues
{
    public CellValue Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var trimmed = text.Trim();
        if (trimmed.Length == 0) return CellValue.Empty;
        if (trimmed.Length > 1 && trimmed[0] == '0' && char.IsDigit(trimmed[1])) return CellValue.Text(text);

        return numbers.TryParse(trimmed, locale.Culture, out var number)
            || numbers.TryParse(trimmed, CultureInfo.InvariantCulture, out number)
            ? CellValue.Number(number)
            : CellValue.Text(text);
    }
}

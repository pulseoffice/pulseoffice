using System.Globalization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public interface ICellEditText
{
    string Number(double number, string formatCode);

    string Value(CellValue value, string formatCode);
}

public sealed class CellEditText(ISpreadsheetLocale locale, INumberFormatParser parser) : ICellEditText
{
    private readonly DateTime _epoch = new(1899, 12, 30);

    public CellEditText() : this(new SystemSpreadsheetLocale(), new NumberFormatParser())
    {
    }

    public string Value(CellValue value, string formatCode) => value.Kind switch
    {
        ValueKind.Number => Number(value.AsNumber(), formatCode),
        ValueKind.Boolean => new CultureBooleanNames(locale.Culture).Name(value.AsBoolean()),
        _ => value.ToString(),
    };

    public string Number(double number, string formatCode)
    {
        var culture = locale.Culture;
        var plain = number.ToString("R", culture);
        if (string.IsNullOrEmpty(formatCode) || !double.IsFinite(number)) return plain;

        var section = parser.Parse(formatCode).Sections.FirstOrDefault();
        if (section is not { ValueKind: FormatValueKind.DateTime }) return plain;

        var letters = section.Tokens.Where(token => !token.IsLiteral).Select(token => token.Text.ToLowerInvariant()).ToList();
        var time = letters.Any(token => token.Contains('h') || token.Contains('s'));
        var date = letters.Any(token => token.Contains('d') || token.Contains('y') || (token.Contains('m') && (!time || token.Length >= 3)));

        if (!date) return time ? Clock(number) : plain;
        if (number < 0 || number > 2958465) return plain;

        var day = _epoch.AddDays(Math.Floor(number));
        var text = Day(day, culture);
        var fraction = number - Math.Floor(number);
        return time || Math.Round(fraction * 86400) > 0 ? $"{text} {Clock(fraction)}" : text;
    }

    private string Day(DateTime day, CultureInfo culture)
    {
        var separator = culture.DateTimeFormat.DateSeparator is "/" or "-" or "." ? culture.DateTimeFormat.DateSeparator : "/";
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        var month = pattern.IndexOf('M', StringComparison.Ordinal);
        var dayIndex = pattern.IndexOf('d', StringComparison.Ordinal);
        var monthFirst = month >= 0 && dayIndex >= 0 && month < dayIndex;
        var order = monthFirst ? $"MM'{separator}'dd'{separator}'yyyy" : $"dd'{separator}'MM'{separator}'yyyy";
        return day.ToString(order, CultureInfo.InvariantCulture);
    }

    private string Clock(double days)
    {
        var seconds = (long)Math.Round(Math.Abs(days) * 86400);
        var sign = days < 0 && seconds > 0 ? "-" : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}");
    }
}

using System.Globalization;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IFilterValueText
{
    string Canonical(string typed);

    string Display(string canonical, bool dates);
}

public sealed class FilterValueText(ISpreadsheetLocale locale, IDateEpoch epochs) : IFilterValueText
{
    private readonly NumericText _typed = new(locale, epochs);

    public FilterValueText(ISpreadsheetLocale locale) : this(locale, new AmbientDateEpoch())
    {
    }

    public string Canonical(string typed)
    {
        ArgumentNullException.ThrowIfNull(typed);
        var text = typed.Trim();
        if (text.Length == 0) return text;
        return _typed.TryReadGrouped(text, out var number) || _typed.TryReadValue(text, out number)
            ? number.ToString("R", CultureInfo.InvariantCulture)
            : text;
    }

    public string Display(string canonical, bool dates)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        if (!double.TryParse(canonical, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            return canonical;
        var culture = locale.Culture;
        if (dates && new CalculationSettings { DateEpoch = epochs.Current }.TryDateOf(number, out var moment))
        {
            return moment.ToString(moment.TimeOfDay == TimeSpan.Zero ? "d" : "g", culture);
        }
        return number.ToString("R", culture);
    }
}

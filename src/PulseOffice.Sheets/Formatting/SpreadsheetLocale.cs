using System.Globalization;

namespace PulseOffice.Sheets.Formatting;

public interface ISpreadsheetLocale
{
    CultureInfo Culture { get; }
}

public sealed class SystemSpreadsheetLocale : ISpreadsheetLocale
{
    public CultureInfo Culture => CultureInfo.CurrentCulture;
}

public sealed class FixedSpreadsheetLocale(CultureInfo culture) : ISpreadsheetLocale
{
    public CultureInfo Culture { get; } = culture ?? throw new ArgumentNullException(nameof(culture));

    public static FixedSpreadsheetLocale Invariant() => new(CultureInfo.InvariantCulture);
}

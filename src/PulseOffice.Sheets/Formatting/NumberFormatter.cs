using System.Globalization;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Formatting;

public sealed record FormattedCell(string Text, string? ColorName, bool AlignRight)
{
    public static readonly FormattedCell Empty = new(string.Empty, null, false);
}

public interface INumberFormatter
{
    FormattedCell Format(CellValue value, NumberFormat format, double columnWidthInCharacters = 12);
}

public sealed class NumberFormatter : INumberFormatter
{
    private readonly IBooleanNames _booleans;
    private readonly GeneralNumberRenderer _general;
    private readonly IReadOnlyDictionary<FormatValueKind, SectionRenderer> _renderers;

    public NumberFormatter(CultureInfo culture, Func<double, DateTime> serialToDate)
        : this(culture, serialToDate, new CultureBooleanNames(culture))
    {
    }

    public NumberFormatter(CultureInfo culture, Func<double, DateTime> serialToDate, IBooleanNames booleans)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(serialToDate);
        _booleans = booleans ?? throw new ArgumentNullException(nameof(booleans));
        _general = new GeneralNumberRenderer(culture);

        _renderers = new SectionRenderer[]
        {
            new PlainNumberRenderer(culture),
            new ScientificRenderer(culture),
            new FractionRenderer(culture),
            new DateTimeRenderer(culture, serialToDate),
        }.ToDictionary(renderer => renderer.Kind);
    }

    public FormattedCell Format(CellValue value, NumberFormat format, double columnWidthInCharacters = 12)
    {
        ArgumentNullException.ThrowIfNull(format);

        return value.Kind switch
        {
            ValueKind.Empty => FormattedCell.Empty,
            ValueKind.Error => new FormattedCell(value.ErrorText, "Red", true),
            ValueKind.Boolean => new FormattedCell(_booleans.Name(value.AsBoolean()), null, false),
            ValueKind.Text => FormatText(value.AsText(), format),
            _ => FormatNumber(value.AsNumber(), format, columnWidthInCharacters),
        };
    }

    private static FormattedCell FormatText(string text, NumberFormat format)
    {
        var section = format.TextSection;
        if (section is null) return new FormattedCell(text, null, false);

        return new FormattedCell(section.Prefix + text + section.Suffix, section.ColorName, false);
    }

    private FormattedCell FormatNumber(double number, NumberFormat format, double widthInCharacters)
    {
        if (format.IsGeneral || !double.IsFinite(number))
            return new FormattedCell(_general.Render(number, widthInCharacters), null, true);

        var section = format.Select(number);
        if (section.ValueKind == FormatValueKind.Text)
            return new FormattedCell(_general.Render(number, widthInCharacters), null, true);

        var value = format.ShowsMagnitude(section) ? Math.Abs(number) : number;
        var renderer = _renderers.GetValueOrDefault(section.ValueKind, _renderers[FormatValueKind.Number]);

        return new FormattedCell(section.Prefix + renderer.Render(value, section) + section.Suffix, section.ColorName, true);
    }
}

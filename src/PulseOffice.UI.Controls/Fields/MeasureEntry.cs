using System.Globalization;

namespace PulseOffice.UI.Controls.Fields;

public sealed record MeasureOptions(string Unit, double Minimum = double.MinValue, double Maximum = double.MaxValue, double Step = 1)
{
    public double? LargeStep { get; init; }

    public bool Integer { get; init; }

    public IReadOnlyList<double> Presets { get; init; } = [];

    public bool ShowSlider { get; init; }
}

public enum MeasureParseOutcome { Accepted, Clamped, Invalid }

public readonly record struct MeasureParse(MeasureParseOutcome Outcome, double Value);

public sealed class MeasureEntry
{
    private readonly IMeasureUnits _units;
    private readonly CultureInfo _culture;
    private readonly NumberEntry _base;

    public MeasureEntry(MeasureOptions options, IMeasureUnits units, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(options);
        _units = units ?? throw new ArgumentNullException(nameof(units));
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));

        Options = options;
        BaseLabel = options.Unit;
        Unit = units.Find(options.Unit) ?? new MeasureUnit(options.Unit, MeasureDimension.Lines, 1, []);
        DisplayUnit = Unit;
        _base = new NumberEntry(new NumericBounds(options.Minimum, options.Maximum, options.Step), options.Integer, culture);
        LargeStep = options.LargeStep is > 0 and var large ? large : _base.Bounds.Step * 10;
    }

    public MeasureOptions Options { get; }

    public MeasureUnit Unit { get; }

    public string BaseLabel { get; }

    public MeasureUnit DisplayUnit { get; private set; }

    public string DisplayLabel => DisplayUnit == Unit ? BaseLabel : DisplayUnit.Symbol;

    public NumericBounds Bounds => _base.Bounds;

    public double LargeStep { get; }

    public IReadOnlyList<MeasureUnit> Alternatives => _units.Find(Unit.Symbol) is null ? [Unit] : _units.CompatibleWith(Unit);

    public void ShowIn(MeasureUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        DisplayUnit = Alternatives.Contains(unit) ? unit : Unit;
    }

    public double Normalize(double value) => _base.Normalize(value);

    public double Step(double value, int direction, bool large) =>
        Normalize(value + direction * (large ? LargeStep : _base.Bounds.Step));

    public string Format(double value) => FormatIn(value, DisplayUnit);

    public string FormatWithUnit(double value) => Format(value) + Separator + DisplayLabel;

    public string FormatBound(double value) => FormatIn(value, DisplayUnit) + Separator + DisplayLabel;

    public MeasureParse Parse(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        var split = trimmed.Length;
        while (split > 0 && !char.IsDigit(trimmed[split - 1])) split--;

        var number = trimmed[..split].Trim();
        var symbol = trimmed[split..].Trim();
        var unit = DisplayUnit;

        if (symbol.Length > 0)
        {
            if (string.Equals(symbol, BaseLabel, StringComparison.OrdinalIgnoreCase)) unit = Unit;
            else if (Alternatives.FirstOrDefault(candidate => candidate.Answers(symbol)) is { } named) unit = named;
            else return new MeasureParse(MeasureParseOutcome.Invalid, 0);
        }

        if (!double.TryParse(number, NumberStyles.Float, _culture, out var typed) || !double.IsFinite(typed))
            return new MeasureParse(MeasureParseOutcome.Invalid, 0);

        var inBase = unit == Unit ? typed : _units.Convert(typed, unit, Unit);
        var normalized = Normalize(inBase);
        var clamped = inBase < Bounds.Minimum - Tolerance || inBase > Bounds.Maximum + Tolerance;
        return new MeasureParse(clamped ? MeasureParseOutcome.Clamped : MeasureParseOutcome.Accepted, normalized);
    }

    private const double Tolerance = 1e-9;

    private string Separator => BaseLabel is "%" or "°" && DisplayUnit == Unit ? string.Empty : " ";

    private string FormatIn(double value, MeasureUnit unit)
    {
        if (unit == Unit) return _base.Format(value);

        var step = _units.Convert(_base.Bounds.Step, Unit, unit);
        var converted = _units.Convert(value, Unit, unit);
        return new NumberEntry(new NumericBounds(double.MinValue, double.MaxValue, Math.Min(step, 0.01)), false, _culture).Format(converted);
    }
}

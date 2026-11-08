using System.Globalization;

namespace PulseOffice.UI.Controls.Fields;

public enum MeasureDimension { Length, Percent, Angle, Time, Lines }

public sealed record MeasureUnit(string Symbol, MeasureDimension Dimension, double Scale, IReadOnlyList<string> Aliases)
{
    public bool Answers(string text) =>
        string.Equals(Symbol, text, StringComparison.OrdinalIgnoreCase)
        || Aliases.Any(alias => string.Equals(alias, text, StringComparison.OrdinalIgnoreCase));
}

public interface IMeasureUnits
{
    IReadOnlyList<MeasureUnit> All { get; }

    MeasureUnit? Find(string? symbol);

    IReadOnlyList<MeasureUnit> CompatibleWith(MeasureUnit unit);

    double Convert(double value, MeasureUnit from, MeasureUnit to);
}

public sealed class MeasureUnits : IMeasureUnits
{
    private const double PointsPerInch = 72;
    private const double PixelsPerInch = 96;

    public IReadOnlyList<MeasureUnit> All { get; } =
    [
        new("mm", MeasureDimension.Length, PointsPerInch / 25.4, []),
        new("cm", MeasureDimension.Length, PointsPerInch / 2.54, []),
        new("pt", MeasureDimension.Length, 1, ["punti", "points", "Punkt"]),
        new("in", MeasureDimension.Length, PointsPerInch, ["\"", "″", "inch", "pollici", "Zoll", "pulg", "po"]),
        new("px", MeasureDimension.Length, PointsPerInch / PixelsPerInch, ["pixel"]),
        new("%", MeasureDimension.Percent, 1, []),
        new("°", MeasureDimension.Angle, 1, ["deg", "gradi"]),
        new("s", MeasureDimension.Time, 1, ["sec"]),
        new("ms", MeasureDimension.Time, 0.001, []),
        new("righe", MeasureDimension.Lines, 1, ["lines", "Zeilen", "líneas", "lignes", "rows"]),
    ];

    public MeasureUnit? Find(string? symbol)
    {
        var trimmed = symbol?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return null;
        return All.FirstOrDefault(unit => string.Equals(unit.Symbol, trimmed, StringComparison.Ordinal))
            ?? All.FirstOrDefault(unit => unit.Answers(trimmed));
    }

    public IReadOnlyList<MeasureUnit> CompatibleWith(MeasureUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return [.. All.Where(candidate => candidate.Dimension == unit.Dimension)];
    }

    public double Convert(double value, MeasureUnit from, MeasureUnit to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (from.Dimension != to.Dimension)
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Cannot convert {from.Symbol} to {to.Symbol}"));
        return from == to ? value : Math.Round(value * from.Scale / to.Scale, 10);
    }
}

public sealed record LabelledUnit(string Label, string Symbol);

public interface IMeasureLabelReader
{
    LabelledUnit? Read(string label);
}

public sealed class MeasureLabelReader(IMeasureUnits units) : IMeasureLabelReader
{
    public LabelledUnit? Read(string label)
    {
        var trimmed = label?.TrimEnd() ?? string.Empty;
        if (!trimmed.EndsWith(')')) return null;

        var open = trimmed.LastIndexOf('(');
        if (open <= 0) return null;

        var symbol = trimmed[(open + 1)..^1].Trim();
        if (units.Find(symbol) is null) return null;

        var text = trimmed[..open].TrimEnd();
        return text.Length == 0 ? null : new LabelledUnit(text, symbol);
    }
}

using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets;

public enum SeriesKind { Linear, Growth, Date, AutoFill }

public enum SeriesDateUnit { Day, Weekday, Month, Year }

public sealed record SeriesRequest(CellRange Range, SeriesKind Kind)
{
    public bool ByColumns { get; init; } = true;

    public double Step { get; init; } = 1;

    public double? Stop { get; init; }

    public SeriesDateUnit DateUnit { get; init; } = SeriesDateUnit.Day;
}

public interface ISeriesFill
{
    IReadOnlyList<(CellAddress Address, CellValue Value)> Plan(Worksheet sheet, SeriesRequest request);
}

public sealed class SeriesFill : ISeriesFill
{
    private readonly IReadOnlyDictionary<SeriesKind, SeriesProgression> _progressions;
    private readonly SeriesProgression _linear = new LinearProgression();

    public SeriesFill()
    {
        var units = new DateStep[] { new DayStep(), new WeekdayStep(), new MonthStep(SeriesDateUnit.Month, 1), new MonthStep(SeriesDateUnit.Year, 12) }
            .ToDictionary(unit => unit.Unit);

        _progressions = new SeriesProgression[] { _linear, new GrowthProgression(), new DateProgression(units) }
            .ToDictionary(progression => progression.Kind);
    }

    public IReadOnlyList<(CellAddress Address, CellValue Value)> Plan(Worksheet sheet, SeriesRequest request)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(request);

        var cells = Ordered(request).ToList();
        if (cells.Count < 2 || !sheet[cells[0]].Value.TryCoerceToNumber(out var start)) return [];

        var progression = _progressions.GetValueOrDefault(request.Kind, _linear);
        var calculation = sheet.Owner?.Calculation ?? CalculationSettings.Default;
        var written = new List<(CellAddress, CellValue)>();

        for (var index = 1; index < cells.Count; index++)
        {
            var value = progression.At(request, start, index, calculation);
            if (!double.IsFinite(value)) break;
            if (request.Stop is { } stop && (request.Step >= 0 ? value > stop : value < stop)) break;

            written.Add((cells[index], CellValue.Number(value)));
        }

        return written;
    }

    private IEnumerable<CellAddress> Ordered(SeriesRequest request)
    {
        var range = request.Range;

        if (request.ByColumns)
        {
            for (var column = range.Start.Column; column <= range.End.Column; column++)
                for (var row = range.Start.Row; row <= range.End.Row; row++)
                    yield return new CellAddress(row, column);

            yield break;
        }

        for (var row = range.Start.Row; row <= range.End.Row; row++)
            for (var column = range.Start.Column; column <= range.End.Column; column++)
                yield return new CellAddress(row, column);
    }
}

internal abstract class SeriesProgression
{
    public abstract SeriesKind Kind { get; }

    public abstract double At(SeriesRequest request, double start, int index, CalculationSettings calculation);
}

internal sealed class LinearProgression : SeriesProgression
{
    public override SeriesKind Kind => SeriesKind.Linear;

    public override double At(SeriesRequest request, double start, int index, CalculationSettings calculation) => start + (request.Step * index);
}

internal sealed class GrowthProgression : SeriesProgression
{
    public override SeriesKind Kind => SeriesKind.Growth;

    public override double At(SeriesRequest request, double start, int index, CalculationSettings calculation) =>
        start * Math.Pow(request.Step == 0 ? 1 : request.Step, index);
}

internal sealed class DateProgression(IReadOnlyDictionary<SeriesDateUnit, DateStep> units) : SeriesProgression
{
    public override SeriesKind Kind => SeriesKind.Date;

    public override double At(SeriesRequest request, double start, int index, CalculationSettings calculation) =>
        (units.TryGetValue(request.DateUnit, out var unit) ? unit : units[SeriesDateUnit.Day])
            .Advance(start, (long)Math.Clamp(Math.Round(request.Step) * index, -MaximumDays, MaximumDays), calculation);

    private const double MaximumDays = 3_652_059;
}

internal abstract class DateStep
{
    public abstract SeriesDateUnit Unit { get; }

    public abstract double Advance(double start, long steps, CalculationSettings calculation);
}

internal sealed class DayStep : DateStep
{
    public override SeriesDateUnit Unit => SeriesDateUnit.Day;

    public override double Advance(double start, long steps, CalculationSettings calculation) => start + steps;
}

internal sealed class WeekdayStep : DateStep
{
    public override SeriesDateUnit Unit => SeriesDateUnit.Weekday;

    public override double Advance(double start, long steps, CalculationSettings calculation)
    {
        if (steps == 0) return start;
        var offset = (int)calculation.DateEpoch.DayOfWeek;
        var value = start;
        var direction = steps < 0 ? -1 : 1;
        var remaining = Math.Abs(steps);
        var stepped = (remaining - 1) % 5 + 1;

        for (var moved = 0L; moved < stepped;)
        {
            value += direction;
            if ((((int)(value % 7) + offset) % 7 + 7) % 7 is not (0 or 6)) moved++;
        }

        return value + direction * 7.0 * ((remaining - stepped) / 5);
    }
}

internal sealed class MonthStep(SeriesDateUnit unit, int months) : DateStep
{
    public override SeriesDateUnit Unit => unit;

    public override double Advance(double start, long steps, CalculationSettings calculation)
    {
        if (!calculation.TryDateOf(start, out var date)) return double.NaN;
        var target = (date.Year * 12L) + date.Month - 1 + (steps * months);
        return target is < 12 or > (9999 * 12) + 11
            ? double.NaN
            : calculation.SerialOf(date.AddMonths((int)(target - ((date.Year * 12L) + date.Month - 1))));
    }
}

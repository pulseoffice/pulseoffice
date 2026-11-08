using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record SparklinePoints(IReadOnlyList<double?> Values, double Minimum, double Maximum)
{
    public static readonly SparklinePoints Empty = new([], 0, 0);

    public bool IsEmpty => Values.Count == 0 || Values.All(value => value is null);
}

public interface ISparklineData
{
    SparklinePoints Read(Worksheet sheet, SparklineGroup group, Sparkline sparkline);
}

public sealed class SparklineData : ISparklineData
{
    private const long FullRangeLimit = 65_536;

    public SparklinePoints Read(Worksheet sheet, SparklineGroup group, Sparkline sparkline)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(sparkline);

        var values = Values(sheet, sparkline.Source);

        if (values.Count == 0) return SparklinePoints.Empty;

        var bounds = group.SameScale ? GroupBounds(sheet, group) : Bounds(values);

        return bounds is { } scale ? new SparklinePoints(values, scale.Minimum, scale.Maximum) : new SparklinePoints(values, 0, 0);
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<SparklineGroup, GroupScale> _groupScales = [];

    private (double Minimum, double Maximum)? GroupBounds(Worksheet sheet, SparklineGroup group)
    {
        if (_groupScales.TryGetValue(group, out var cached) && ReferenceEquals(cached.Sheet, sheet) && cached.Revision == sheet.Revision)
            return cached.Bounds;

        var bounds = Bounds(group.Sparklines.SelectMany(other => Values(sheet, other.Source)));
        _groupScales.AddOrUpdate(group, new GroupScale(sheet, sheet.Revision, bounds));
        return bounds;
    }

    private (double Minimum, double Maximum)? Bounds(IEnumerable<double?> scope)
    {
        var any = false;
        var lowest = double.PositiveInfinity;
        var highest = double.NegativeInfinity;
        foreach (var value in scope)
        {
            if (value is not { } number) continue;
            any = true;
            lowest = Math.Min(lowest, number);
            highest = Math.Max(highest, number);
        }

        if (!any) return null;
        var minimum = Math.Min(0, lowest);
        return (minimum, Math.Max(highest, minimum + 1e-9));
    }

    private sealed record GroupScale(Worksheet Sheet, long Revision, (double Minimum, double Maximum)? Bounds);

    private List<double?> Values(Worksheet sheet, CellRange range)
    {
        var values = new List<double?>();
        if ((long)range.RowCount * range.ColumnCount > FullRangeLimit)
        {
            var bottom = Math.Min(range.End.Row, sheet.LastUsedRow);
            var right = Math.Min(range.End.Column, sheet.LastUsedColumn);
            if (bottom < range.Start.Row || right < range.Start.Column) return values;
            range = new CellRange(range.Start, new CellAddress(bottom, right));
        }

        foreach (var address in range.Cells())
        {
            var value = sheet[address].Value;

            values.Add(value.Kind == ValueKind.Number ? value.AsNumber() : null);
        }

        return values;
    }
}

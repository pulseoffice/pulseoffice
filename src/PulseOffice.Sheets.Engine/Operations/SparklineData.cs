using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record SparklinePoints(IReadOnlyList<double?> Values, double Minimum, double Maximum)
{
    public static readonly SparklinePoints Empty = new([], 0, 0);

    public bool IsEmpty => Values.Count == 0 || Values.All(value => value is null);

    public bool SpansGaps { get; init; }

    public bool RightToLeft { get; init; }

    public IReadOnlyList<double>? Positions { get; init; }

    public double Position(int index)
    {
        var count = Values.Count;
        var share = Positions is { } positions && index < positions.Count ? positions[index]
            : count == 1 ? 0.5 : (double)index / (count - 1);
        return RightToLeft ? 1 - share : share;
    }
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

        var cells = Cells(sheet, group, sparkline);
        if (cells.Count == 0) return SparklinePoints.Empty;
        var values = cells.Select(cell => cell.Value).ToList();

        var own = Bounds(values);
        var shared = group.MinimumAxis == SparklineAxisBound.Group || group.MaximumAxis == SparklineAxisBound.Group ? GroupBounds(sheet, group) : null;
        var minimum = group.MinimumAxis switch
        {
            SparklineAxisBound.Custom when group.ManualMinimum is { } manual => manual,
            SparklineAxisBound.Group => shared?.Minimum,
            _ => own?.Minimum,
        };
        var maximum = group.MaximumAxis switch
        {
            SparklineAxisBound.Custom when group.ManualMaximum is { } manual => manual,
            SparklineAxisBound.Group => shared?.Maximum,
            _ => own?.Maximum,
        };

        var points = minimum is { } low && maximum is { } high
            ? new SparklinePoints(values, low, Math.Max(high, low + 1e-9))
            : new SparklinePoints(values, 0, 0);
        return points with
        {
            SpansGaps = group.EmptyCells == SparklineEmptyCells.Span,
            RightToLeft = group.RightToLeft,
            Positions = DatePositions(sheet, group, cells),
        };
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<SparklineGroup, GroupScale> _groupScales = [];

    private (double Minimum, double Maximum)? GroupBounds(Worksheet sheet, SparklineGroup group)
    {
        var stamp = Stamp(sheet, group);
        if (_groupScales.TryGetValue(group, out var cached) && ReferenceEquals(cached.Sheet, sheet) && cached.Revision == stamp)
            return cached.Bounds;

        var bounds = Bounds(group.Sparklines.SelectMany(other => Cells(sheet, group, other).Select(cell => cell.Value)));
        _groupScales.AddOrUpdate(group, new GroupScale(sheet, stamp, bounds));
        return bounds;
    }

    private long Stamp(Worksheet sheet, SparklineGroup group) =>
        sheet.Revision + group.Sparklines
            .Select(sparkline => sparkline.SourceSheet)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Sum(name => sheet.Owner?.FindSheet(name)?.Revision ?? 0);

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

        return any ? (lowest, highest) : null;
    }

    private IReadOnlyList<double>? DatePositions(Worksheet sheet, SparklineGroup group, List<(int Ordinal, double? Value)> cells)
    {
        if (group.DateRange is not { } range || Source(sheet, group.DateSheet) is not { } dates) return null;
        var all = range.Cells().Take(cells[^1].Ordinal + 1)
            .Select(address => dates[address].Value is { Kind: ValueKind.Number } value ? value.AsNumber() : double.NaN).ToList();
        var stamps = cells.Select(cell => cell.Ordinal < all.Count ? all[cell.Ordinal] : double.NaN).ToList();
        if (stamps.Any(stamp => !double.IsFinite(stamp))) return null;
        var first = stamps.Min();
        var span = stamps.Max() - first;
        return span <= 0 ? null : stamps.Select(stamp => (stamp - first) / span).ToList();
    }

    private sealed record GroupScale(Worksheet Sheet, long Revision, (double Minimum, double Maximum)? Bounds);

    private Worksheet? Source(Worksheet sheet, string? name) =>
        name is null ? sheet : sheet.Owner?.FindSheet(name);

    private List<(int Ordinal, double? Value)> Cells(Worksheet host, SparklineGroup group, Sparkline sparkline)
    {
        var cells = new List<(int Ordinal, double? Value)>();
        if (Source(host, sparkline.SourceSheet) is not { } sheet) return cells;
        var range = sparkline.Source;
        if ((long)range.RowCount * range.ColumnCount > FullRangeLimit)
        {
            var bottom = Math.Min(range.End.Row, sheet.LastUsedRow);
            var right = Math.Min(range.End.Column, sheet.LastUsedColumn);
            if (bottom < range.Start.Row || right < range.Start.Column) return cells;
            range = new CellRange(range.Start, new CellAddress(bottom, right));
        }

        var ordinal = -1;
        foreach (var address in range.Cells())
        {
            ordinal++;
            if (!group.ShowHidden && (sheet.Layout.IsRowHidden(address.Row) || sheet.Layout.IsColumnHidden(address.Column))) continue;
            var value = sheet[address].Value;
            cells.Add((ordinal, value.Kind == ValueKind.Number ? value.AsNumber()
                : value.Kind == ValueKind.Empty && group.EmptyCells == SparklineEmptyCells.Zero ? 0 : null));
        }

        return cells;
    }
}

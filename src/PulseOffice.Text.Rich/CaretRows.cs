namespace PulseOffice.Text.Rich;

public readonly record struct CaretStop(int Index, double X);

public sealed record CaretRow(IReadOnlyList<CaretStop> Stops)
{
    public int First => Stops[0].Index;

    public int Last => Stops[^1].Index;

    public bool Contains(int index) => Stops.Any(stop => stop.Index == index);

    public CaretStop Nearest(double x) => Stops.MinBy(stop => Math.Abs(stop.X - x));

    public double XOf(int index) => Stops.FirstOrDefault(stop => stop.Index == index, Stops[0]).X;
}

public enum CaretMove
{
    Up,
    Down,
    RowStart,
    RowEnd,
    PageUp,
    PageDown,
}

public readonly record struct CaretTarget(int Index, double? PreferredX);

public interface ICaretRowNavigation
{
    CaretTarget Move(IReadOnlyList<CaretRow> rows, int caret, double? preferredX, CaretMove move, int pageRows);
}

public sealed class CaretRowNavigation : ICaretRowNavigation
{
    public CaretTarget Move(IReadOnlyList<CaretRow> rows, int caret, double? preferredX, CaretMove move, int pageRows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var usable = rows.Where(row => row.Stops.Count > 0).ToList();
        if (usable.Count == 0) return new CaretTarget(caret, null);

        var current = RowOf(usable, caret);
        var row = usable[current];
        var x = preferredX ?? row.XOf(caret);

        return move switch
        {
            CaretMove.RowStart => new CaretTarget(row.First, null),
            CaretMove.RowEnd => new CaretTarget(RowEnd(usable, current), null),
            CaretMove.Up => Vertical(usable, current, -1, x, usable[0].First),
            CaretMove.Down => Vertical(usable, current, 1, x, usable[^1].Last),
            CaretMove.PageUp => Vertical(usable, current, -Math.Max(1, pageRows), x, usable[0].First),
            CaretMove.PageDown => Vertical(usable, current, Math.Max(1, pageRows), x, usable[^1].Last),
            _ => new CaretTarget(caret, preferredX),
        };
    }

    private CaretTarget Vertical(IReadOnlyList<CaretRow> rows, int current, int step, double x, int edge)
    {
        var target = current + step;
        if (target < 0 || target >= rows.Count)
        {
            if (Math.Abs(step) == 1 || target < 0 && current == 0 || target >= rows.Count && current == rows.Count - 1)
                return new CaretTarget(edge, x);
            target = Math.Clamp(target, 0, rows.Count - 1);
        }
        return new CaretTarget(rows[target].Nearest(x).Index, x);
    }

    private int RowEnd(IReadOnlyList<CaretRow> rows, int current)
    {
        var row = rows[current];
        var shared = current + 1 < rows.Count && rows[current + 1].First == row.Last;
        return shared && row.Stops.Count > 1 ? row.Stops[^2].Index : row.Last;
    }

    private int RowOf(IReadOnlyList<CaretRow> rows, int caret)
    {
        for (var index = rows.Count - 1; index >= 0; index--)
            if (rows[index].Contains(caret)) return index;
        for (var index = rows.Count - 1; index >= 0; index--)
            if (rows[index].First <= caret) return index;
        return 0;
    }
}

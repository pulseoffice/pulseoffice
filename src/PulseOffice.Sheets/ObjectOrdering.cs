namespace PulseOffice.Sheets;

public enum ObjectOrderMove
{
    Forward,
    Backward,
    ToFront,
    ToBack,
}

public interface IObjectOrdering
{
    IReadOnlyList<PlacedObject> Drawn(Worksheet sheet);

    IReadOnlyList<PlacedObject> Reordered(
        Worksheet sheet, IReadOnlyCollection<PlacedObject> moving, ObjectOrderMove move);

    int NextOrder(Worksheet sheet);
}

public sealed class ObjectOrdering : IObjectOrdering
{
    public IReadOnlyList<PlacedObject> Drawn(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return [.. sheet.Objects
            .Select((placed, index) => (placed, index))
            .OrderBy(entry => entry.placed.ZOrder)
            .ThenBy(entry => entry.index)
            .Select(entry => entry.placed)];
    }

    public int NextOrder(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return sheet.Objects.Count == 0 ? 0 : sheet.Objects.Max(placed => placed.ZOrder) + 1;
    }

    public IReadOnlyList<PlacedObject> Reordered(
        Worksheet sheet, IReadOnlyCollection<PlacedObject> moving, ObjectOrderMove move)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(moving);

        var ordered = Drawn(sheet).ToList();
        if (moving.Count == 0 || ordered.Count < 2) return ordered;

        var selected = new HashSet<PlacedObject>(moving, Identity.Comparer);

        var result = move switch
        {
            ObjectOrderMove.ToFront => [.. ordered.Where(o => !selected.Contains(o)), .. ordered.Where(selected.Contains)],
            ObjectOrderMove.ToBack => [.. ordered.Where(selected.Contains), .. ordered.Where(o => !selected.Contains(o))],
            ObjectOrderMove.Forward => Shifted(ordered, selected, forward: true),
            _ => Shifted(ordered, selected, forward: false),
        };

        return [.. result.Select((placed, index) => placed with { ZOrder = index })];
    }

    private static List<PlacedObject> Shifted(
        List<PlacedObject> ordered, HashSet<PlacedObject> selected, bool forward)
    {
        var result = new List<PlacedObject>(ordered);

        var indexes = Enumerable.Range(0, result.Count).Where(index => selected.Contains(result[index]));
        if (forward) indexes = indexes.Reverse();

        foreach (var index in indexes.ToList())
        {
            var target = forward ? index + 1 : index - 1;

            if (target < 0 || target >= result.Count) continue;
            if (selected.Contains(result[target])) continue;

            (result[index], result[target]) = (result[target], result[index]);
        }

        return result;
    }
}

internal sealed class Identity : IEqualityComparer<PlacedObject>
{
    public static readonly IEqualityComparer<PlacedObject> Comparer = new Identity();

    public bool Equals(PlacedObject? first, PlacedObject? second) => ReferenceEquals(first, second);

    public int GetHashCode(PlacedObject placed) =>
        System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(placed);
}

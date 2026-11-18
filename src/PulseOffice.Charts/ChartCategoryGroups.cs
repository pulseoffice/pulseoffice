namespace PulseOffice.Charts;

public sealed record ChartCategoryGroup(int First, int Count, string Label)
{
    public int Last => First + Count - 1;
}

public interface IChartCategoryGroups
{
    IReadOnlyList<IReadOnlyList<ChartCategoryGroup>> Groups(ChartData data);

    IReadOnlyList<IReadOnlyList<string>> Compact(IReadOnlyList<IReadOnlyList<string>> paths, int levels);
}

public sealed class ChartCategoryGroups : IChartCategoryGroups
{
    public IReadOnlyList<IReadOnlyList<ChartCategoryGroup>> Groups(ChartData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.CategoryLevels is not { Count: > 0 } levels) return [];

        var count = data.Categories.Count;
        var starts = new bool[count];
        var result = new List<IReadOnlyList<ChartCategoryGroup>>();
        foreach (var level in levels)
        {
            for (var index = 0; index < count; index++)
                starts[index] |= index == 0 || (index < level.Count && level[index].Length > 0);

            var groups = new List<ChartCategoryGroup>();
            for (var index = 0; index < count; index++)
            {
                if (!starts[index]) continue;
                var end = index + 1;
                while (end < count && !starts[end]) end++;
                groups.Add(new ChartCategoryGroup(index, end - index, index < level.Count ? level[index] : string.Empty));
            }
            result.Add(groups);
        }
        return result;
    }

    public IReadOnlyList<IReadOnlyList<string>> Compact(IReadOnlyList<IReadOnlyList<string>> paths, int levels)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var result = Enumerable.Range(0, Math.Max(levels, 0)).Select(_ => new List<string>(paths.Count)).ToList();
        for (var index = 0; index < paths.Count; index++)
        {
            var changed = index == 0;
            for (var level = 0; level < result.Count; level++)
            {
                var label = Label(paths[index], level);
                changed |= index > 0 && !string.Equals(label, Label(paths[index - 1], level), StringComparison.Ordinal);
                result[level].Add(changed ? label : string.Empty);
            }
        }
        return result;
    }

    private string Label(IReadOnlyList<string> path, int level) => level < path.Count ? path[level] : string.Empty;
}

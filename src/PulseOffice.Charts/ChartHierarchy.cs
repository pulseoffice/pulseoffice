namespace PulseOffice.Charts;

public sealed record ChartHierarchyNode(string Label, double Value, int Point, IReadOnlyList<ChartHierarchyNode> Children)
{
    public bool IsLeaf => Point >= 0;
}

public sealed record ChartCategoryBucket(string Label, IReadOnlyList<int> Points);

public interface IChartHierarchy
{
    IReadOnlyList<ChartHierarchyNode> Roots(ChartData data);

    IReadOnlyList<ChartCategoryBucket> Buckets(ChartData data);
}

public sealed class ChartHierarchy : IChartHierarchy
{
    public IReadOnlyList<ChartHierarchyNode> Roots(ChartData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var values = data.Series.FirstOrDefault()?.Values ?? [];
        var leaves = Enumerable.Range(0, values.Count)
            .Select(index => (Point: index, Value: double.IsFinite(values[index]) ? Math.Abs(values[index]) : 0))
            .Where(entry => entry.Value > 0)
            .ToList();
        var paths = Paths(data, values.Count);
        return Build(leaves, paths, 0);
    }

    public IReadOnlyList<ChartCategoryBucket> Buckets(ChartData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var count = data.Series.Select(series => series.Values.Count).DefaultIfEmpty(0).Max();
        if (data.OrdinalCategories || data.Categories.Count == 0) return [new ChartCategoryBucket(string.Empty, [.. Enumerable.Range(0, count)])];

        var order = new List<string>();
        var points = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            var label = index < data.Categories.Count ? data.Categories[index] : string.Empty;
            if (!points.TryGetValue(label, out var list))
            {
                points[label] = list = [];
                order.Add(label);
            }
            list.Add(index);
        }
        return [.. order.Select(label => new ChartCategoryBucket(label, points[label]))];
    }

    private List<string[]> Paths(ChartData data, int count)
    {
        var levels = data.CategoryLevels ?? [];
        var paths = new List<string[]>(count);
        var carried = new string[levels.Count];
        for (var index = 0; index < count; index++)
        {
            var path = new string[levels.Count + 1];
            for (var level = 0; level < levels.Count; level++)
            {
                var label = index < levels[level].Count ? levels[level][index] : string.Empty;
                if (label.Length > 0)
                {
                    if (!string.Equals(carried[level], label, StringComparison.Ordinal))
                        for (var deeper = level + 1; deeper < levels.Count; deeper++) carried[deeper] = string.Empty;
                    carried[level] = label;
                }
                path[level] = carried[level] ?? string.Empty;
            }
            path[levels.Count] = index < data.Categories.Count ? data.Categories[index] : string.Empty;
            paths.Add(path);
        }
        return paths;
    }

    private List<ChartHierarchyNode> Build(IReadOnlyList<(int Point, double Value)> leaves, IReadOnlyList<string[]> paths, int depth)
    {
        var nodes = new List<ChartHierarchyNode>();
        var index = 0;
        while (index < leaves.Count)
        {
            var path = paths[leaves[index].Point];
            if (depth >= path.Length - 1)
            {
                nodes.Add(new ChartHierarchyNode(path[^1], leaves[index].Value, leaves[index].Point, []));
                index++;
                continue;
            }

            var label = path[depth];
            var end = index + 1;
            while (end < leaves.Count && SamePrefix(paths[leaves[end].Point], path, depth)) end++;
            var group = leaves.Skip(index).Take(end - index).ToList();
            var children = Build(group, paths, depth + 1);
            if (label.Length == 0) nodes.AddRange(children);
            else nodes.Add(new ChartHierarchyNode(label, group.Sum(entry => entry.Value), -1, children));
            index = end;
        }
        return nodes;
    }

    private bool SamePrefix(string[] path, string[] other, int depth)
    {
        for (var level = 0; level <= depth; level++)
            if (!string.Equals(path[level], other[level], StringComparison.Ordinal)) return false;
        return true;
    }
}

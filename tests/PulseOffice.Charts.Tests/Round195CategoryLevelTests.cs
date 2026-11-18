using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public class Round195CategoryLevelTests
{
    private readonly ChartCategoryGroups _groups = new();

    [Fact]
    public void Outer_levels_group_categories_and_inner_groups_break_at_outer_starts()
    {
        var data = new ChartData(["Q1", "Q2", "Q1", "Q2"], [new ChartSeries("S", [1, 2, 3, 4])])
            .WithCategoryLevels([["2025", "", "2026", ""], ["H1", "", "", ""]]);

        var groups = _groups.Groups(data);

        Assert.Equal([new ChartCategoryGroup(0, 2, "2025"), new ChartCategoryGroup(2, 2, "2026")], groups[0]);
        Assert.Equal([new ChartCategoryGroup(0, 2, "H1"), new ChartCategoryGroup(2, 2, string.Empty)], groups[1]);
    }

    [Fact]
    public void Levels_that_do_not_match_the_categories_are_dropped()
    {
        var data = new ChartData(["a", "b"], []).WithCategoryLevels([["x"]]);

        Assert.False(data.HasCategoryLevels);
        Assert.Empty(_groups.Groups(data));
    }

    [Fact]
    public void Full_paths_compact_to_labels_at_group_starts()
    {
        var levels = _groups.Compact([["A", "x"], ["A", "y"], ["B", "x"]], 1);

        Assert.Equal(["A", "", "B"], Assert.Single(levels));
    }
}

using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public sealed class Round334ChartLayoutTests
{
    private readonly IChartStatistics _statistics = new ChartStatistics();
    private readonly IChartHierarchy _hierarchy = new ChartHierarchy();

    [Fact]
    public void Exclusive_quartiles_follow_the_n_plus_one_rule()
    {
        var summary = _statistics.Summary([1, 2, 3, 4, 5, 6, 7], ChartQuartileMethod.Exclusive);
        Assert.Equal(2, summary.LowerQuartile, 9);
        Assert.Equal(6, summary.UpperQuartile, 9);
        Assert.Equal(2.5, _statistics.Summary([1, 2, 3, 4, 5, 6, 7], ChartQuartileMethod.Inclusive).LowerQuartile, 9);
    }

    [Fact]
    public void Bin_width_and_overflow_shape_the_histogram()
    {
        var bins = _statistics.Histogram([1, 2, 3, 4, 10], new ChartStatisticalLayout { BinWidth = 2, Underflow = 1, Overflow = 5 });
        Assert.Equal(4, bins.Count);
        Assert.Equal([1, 2, 1, 1], bins.Select(bin => bin.Count));
    }

    [Fact]
    public void A_waterfall_subtotal_starts_from_zero_and_resets_the_running_total()
    {
        var steps = _statistics.Waterfall([10, -3, 7, 5], [2]);
        Assert.Equal(new WaterfallStep(0, 7, true), steps[2]);
        Assert.Equal(new WaterfallStep(7, 12, false), steps[3]);
    }

    [Fact]
    public void Category_levels_group_leaves_into_parents()
    {
        var data = new ChartData(["a", "b", "c"], [new ChartSeries("s", [1, 2, 3])])
            .WithCategoryLevels([["X", "", "Y"]]);
        var roots = _hierarchy.Roots(data);
        Assert.Equal(["X", "Y"], roots.Select(root => root.Label));
        Assert.Equal(3, roots[0].Value);
        Assert.Equal([0, 1], roots[0].Children.Select(child => child.Point));
    }

    [Fact]
    public void Repeated_parent_labels_also_group()
    {
        var data = new ChartData(["a", "b"], [new ChartSeries("s", [1, 2])]).WithCategoryLevels([["X", "X"]]);
        Assert.Single(_hierarchy.Roots(data));
    }

    [Fact]
    public void Box_buckets_gather_points_by_category()
    {
        var data = new ChartData(["A", "B", "A"], [new ChartSeries("s", [1, 2, 3])]);
        var buckets = _hierarchy.Buckets(data);
        Assert.Equal(2, buckets.Count);
        Assert.Equal([0, 2], buckets[0].Points);
        Assert.Single(_hierarchy.Buckets(data with { OrdinalCategories = true }));
    }
}

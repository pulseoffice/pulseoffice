using Avalonia;
using PulseOffice.UI.Controls.Charts.Plots;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public class Round190ReversedCategoryTests
{
    [Fact]
    public void Reversed_radar_spokes_turn_the_other_way()
    {
        var normal = new RadarSpokes(new Point(0, 0), 4, false);
        var reversed = new RadarSpokes(new Point(0, 0), 4, true);

        Assert.True(normal.Corner(10, 1).X > 0);
        Assert.True(reversed.Corner(10, 1).X < 0);
        Assert.Equal(normal.Corner(10, 0).Y, reversed.Corner(10, 0).Y, 6);
    }

    [Fact]
    public void Statistical_and_financial_plots_draw_reversed_categories()
    {
        Assert.True(new StatisticalPlot().DrawsReversedCategories);
        Assert.True(new FinancialPlot().DrawsReversedCategories);
    }
}

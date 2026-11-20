using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public class Round324ChartAxisPageTests
{
    [Theory]
    [InlineData(ChartType.Radar, true)]
    [InlineData(ChartType.FilledRadar, true)]
    [InlineData(ChartType.Column, true)]
    [InlineData(ChartType.Pie, false)]
    [InlineData(ChartType.Treemap, false)]
    public void The_axis_scale_page_is_offered_wherever_the_plot_honours_the_scales(ChartType type, bool scales) =>
        Assert.Equal(scales, new ChartRenderer().ScalesValues(type));
}

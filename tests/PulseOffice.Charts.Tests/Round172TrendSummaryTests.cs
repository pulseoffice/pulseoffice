using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public class Round172TrendSummaryTests
{
    private readonly ChartTrendFit _fit = new();

    [Fact]
    public void A_linear_trend_gives_its_equation_and_a_perfect_r_squared()
    {
        var summary = _fit.Summary(new ChartTrendline(TrendlineKind.Linear), [(1, 5), (2, 7), (3, 9)]);

        Assert.NotNull(summary);
        Assert.Equal("y = 2x + 3", summary.Equation);
        Assert.Equal(1, summary.RSquared, 9);
    }

    [Fact]
    public void A_quadratic_trend_writes_every_power()
    {
        var summary = _fit.Summary(new ChartTrendline(TrendlineKind.Polynomial) { Order = 2 }, [(0, 1), (1, 0), (2, 1), (3, 4)]);

        Assert.NotNull(summary);
        Assert.Equal("y = 1x^2 - 2x + 1", summary.Equation);
    }

    [Fact]
    public void A_moving_average_has_no_equation()
    {
        Assert.Null(_fit.Summary(new ChartTrendline(TrendlineKind.MovingAverage), [(1, 1), (2, 2), (3, 3)]));
    }
}

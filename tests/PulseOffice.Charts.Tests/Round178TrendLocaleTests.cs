using System.Globalization;
using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public class Round178TrendLocaleTests
{
    private readonly ChartTrendFit _fit = new();

    [Fact]
    public void Equation_coefficients_follow_the_given_culture()
    {
        var summary = _fit.Summary(new ChartTrendline(TrendlineKind.Linear), [(1, 1.5), (2, 3), (3, 4.5)], CultureInfo.GetCultureInfo("it-IT"));

        Assert.Equal("y = 1,5x + 0", summary!.Equation);
    }

    [Fact]
    public void Without_a_culture_the_equation_stays_invariant()
    {
        var summary = _fit.Summary(new ChartTrendline(TrendlineKind.Linear), [(1, 1.5), (2, 3), (3, 4.5)]);

        Assert.Equal("y = 1.5x + 0", summary!.Equation);
    }
}

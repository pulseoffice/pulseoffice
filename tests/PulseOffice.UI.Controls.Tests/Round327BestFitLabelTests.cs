using Avalonia;
using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts.Plots;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round327BestFitLabelTests
{
    private static readonly Size Box = new(10, 8);

    private readonly ChartLabelLayout _layout = new();

    [Fact]
    public void Best_fit_keeps_the_automatic_place_when_it_is_free()
    {
        var label = new ChartDataLabel(new Point(50, 50), 1);

        Assert.Equal(_layout.Origin(label, Box), _layout.BestFit(label, Box, []));
    }

    [Fact]
    public void Best_fit_moves_a_label_off_one_already_drawn()
    {
        var label = new ChartDataLabel(new Point(50, 50), 1);
        var taken = new Rect(_layout.Origin(label, Box), Box);

        var origin = _layout.BestFit(label, Box, [taken]);

        Assert.False(taken.Intersects(new Rect(origin, Box)));
    }
}

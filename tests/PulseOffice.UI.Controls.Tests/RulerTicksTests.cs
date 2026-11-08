using PulseOffice.UI.Controls.Rulers;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class RulerTicksTests
{
    private const double Centimetre = 72 / 2.54;

    private static RulerModel Vertical(double scale) =>
        new(RulerOrientation.Vertical, 0, scale, 842, 57, 785, [], Centimetre);

    [Fact]
    public void VerticalTicksLeaveRoomForTheLabelHeight()
    {
        var ticks = new RulerTicks(6).Plan(Vertical(96 / 72.0), 0, 842, 9);
        var gaps = ticks.Zip(ticks.Skip(1), (a, b) => (b.Points - a.Points) * 96 / 72.0);
        Assert.All(gaps, gap => Assert.True(gap >= 11, $"gap {gap:0.0} px"));
        Assert.Contains(ticks, tick => tick.Kind == RulerTickKind.Half);
    }

    [Fact]
    public void HorizontalTicksKeepQuartersWhenThereIsRoom()
    {
        var model = Vertical(96 / 72.0) with { Orientation = RulerOrientation.Horizontal };
        var ticks = new RulerTicks(6).Plan(model, 0, 595, 5.5);
        Assert.Contains(ticks, tick => tick.Kind == RulerTickKind.Minor);
    }

    [Fact]
    public void LabelsAreThinnedWhenZoomedOut()
    {
        var ticks = new RulerTicks(6).Plan(Vertical(0.5), 0, 842, 11.5);
        Assert.DoesNotContain(ticks, tick => tick.Kind == RulerTickKind.Label && tick.Value % 2 == 1);
    }
}

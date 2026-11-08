using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests.Shapes;

public sealed class ShapeEffectBoundsTests
{
    private readonly ShapeEffectBounds _bounds = new();

    [Theory]
    [InlineData(12, 12)]
    [InlineData(-5, 0)]
    [InlineData(1e9, ShapeEffectBounds.MaximumRadiusPoints)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(double.NaN, 0)]
    public void An_effect_radius_from_a_damaged_file_is_capped(double points, double expected) =>
        Assert.Equal(expected, _bounds.Radius(points));

    [Theory]
    [InlineData(0.4, 0.4)]
    [InlineData(7, 1)]
    [InlineData(-1, 0)]
    [InlineData(double.NaN, 1)]
    public void An_effect_opacity_stays_between_zero_and_one(double opacity, double expected) =>
        Assert.Equal(expected, _bounds.Opacity(opacity));
}

using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round276ShapeFitTests
{
    private static PulseOffice.Testing.UiThreadSession Session => MacroEventsViewTests.Session;

    private static ShapeObject Fitted(TextFlow flow) => new()
    {
        Id = "s",
        Shape = ShapeKind.Rectangle,
        Text = "Uno\nDue\nTre",
        Style = ShapeStyle.Default with { Text = ShapeTextStyle.Default with { Autofit = TextAutofit.ResizeShape, Layout = TextLayout.Default with { Flow = flow } } },
    };

    [Fact]
    public Task Vertical_text_grows_the_width_and_keeps_the_height() => Session.Dispatch(() =>
    {
        var size = new DocumentSize(Twip.FromPoints(10), Twip.FromPoints(200));

        var fitted = new ShapeTextFit().Size(Fitted(TextFlow.Vertical), size)!.Value;

        Assert.Equal(size.Height, fitted.Height);
        Assert.True(fitted.Width.Value > size.Width.Value);
        Assert.Null(new ShapeTextFit().Height(Fitted(TextFlow.Vertical), size));
    }, CancellationToken.None);

    [Fact]
    public Task Horizontal_text_keeps_the_width_and_fits_the_height() => Session.Dispatch(() =>
    {
        var size = new DocumentSize(Twip.FromPoints(200), Twip.FromPoints(10));
        var fit = new ShapeTextFit();

        var fitted = fit.Size(Fitted(TextFlow.Horizontal), size)!.Value;

        Assert.Equal(size.Width, fitted.Width);
        Assert.Equal(fit.Height(Fitted(TextFlow.Horizontal), size), fitted.Height);
    }, CancellationToken.None);

    [Theory]
    [InlineData(ShapeKind.Hexagon)]
    [InlineData(ShapeKind.Star5)]
    [InlineData(ShapeKind.ArrowRight)]
    [InlineData(ShapeKind.Cross)]
    [InlineData(ShapeKind.FlowPredefinedProcess)]
    [InlineData(ShapeKind.FlowCollate)]
    [InlineData(ShapeKind.Bevel)]
    [InlineData(ShapeKind.FoldedCorner)]
    public void Common_presets_have_an_inner_text_area_inside_the_box(ShapeKind kind)
    {
        var margins = new ShapeTextArea().Margins(Fitted(TextFlow.Horizontal) with { Shape = kind }, 100, 60);

        Assert.True(margins.Across + margins.Down > 0);
        Assert.True(margins.Across < 100 && margins.Down < 60);
    }
}

using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class ShapeRichTextStyleTests
{
    private readonly ShapeRichText _bridge = new();

    private ShapeObject StyledShape() => new()
    {
        Id = "s1",
        Shape = ShapeKind.Rectangle,
        Text = "Hello",
        Style = ShapeStyle.Default with { Text = ShapeTextStyle.Default with { Bold = true, Italic = true } },
    };

    private RichText WithoutBold(RichText rich) =>
        rich with { Paragraphs = [.. rich.Paragraphs.Select(paragraph => paragraph.WithRuns([.. paragraph.Runs.Select(run => run with { Bold = false })]))] };

    [Fact]
    public void RichTextShowsTheBoldAndItalicOfTheShapeStyle()
    {
        var run = Assert.Single(_bridge.ToRich(StyledShape()).Paragraphs[0].Runs);

        Assert.True(run.Bold);
        Assert.True(run.Italic);
    }

    [Fact]
    public void RemovingBoldFromAStyleBoldShapeTakesEffect()
    {
        var shape = StyledShape();

        var edited = _bridge.FromRich(shape, WithoutBold(_bridge.ToRich(shape)));

        Assert.False(edited.Style.Text.Bold);
        Assert.All(edited.Runs ?? [], run => Assert.False(run.Bold));
        Assert.True(edited.Style.Text.Italic);
    }

    [Fact]
    public void RoundTripKeepsTheStyleBold()
    {
        var shape = StyledShape();

        var edited = _bridge.FromRich(shape, _bridge.ToRich(shape));

        Assert.True(edited.Style.Text.Bold);
        Assert.All(edited.Runs ?? [], run => Assert.False(run.Bold));
    }
}

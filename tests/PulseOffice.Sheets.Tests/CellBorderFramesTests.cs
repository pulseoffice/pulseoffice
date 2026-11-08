using PulseOffice.Document.Borders;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class CellBorderFramesTests
{
    private readonly CellBorderFrames _frames = new();
    private readonly BorderFrameResolver _resolver = new();

    [Theory]
    [InlineData(0.75, BorderStyle.Thin)]
    [InlineData(1.5, BorderStyle.Medium)]
    [InlineData(3, BorderStyle.Thick)]
    public void LoSpessoreDecideLoStileContinuo(double points, BorderStyle expected)
    {
        Assert.Equal(expected, _frames.Edge(new BorderStroke(BorderDash.Solid, points, "#000000")).Style);
    }

    [Fact]
    public void ITrattiDiventanoStiliDiLedger()
    {
        Assert.Equal(BorderStyle.Dotted, _frames.Edge(new BorderStroke(BorderDash.Dotted, 1, "#000000")).Style);
        Assert.Equal(BorderStyle.Double, _frames.Edge(new BorderStroke(BorderDash.Double, 2, "#000000")).Style);
        Assert.Equal(BorderEdge.None, _frames.Edge(null));
    }

    [Fact]
    public void LeDiagonaliEILatiFannoIlGiro()
    {
        var borders = new CellBorders(
            new BorderEdge(BorderStyle.Thin, "#112233", Twip.FromPoints(0.75)), BorderEdge.None, BorderEdge.None,
            new BorderEdge(BorderStyle.Dashed, "#DC2626", Twip.FromPoints(1)))
        {
            DiagonalUp = new BorderEdge(BorderStyle.Medium, "#000000", Twip.FromPoints(1.5)),
        };

        var frame = _frames.Frame(borders, false, false);
        var rebuilt = _frames.Apply(CellBorders.None, _resolver.Resolve(BorderFrame.Empty, frame, new CellPlacement(true, true, true, true)));

        Assert.Equal(borders, rebuilt);
    }

    [Fact]
    public void LIntervalloMetteGliInterniFraLeCelle()
    {
        var stroke = new BorderStroke(BorderDash.Solid, 0.75, "#000000");
        var after = BorderFrame.Empty.With(BorderPosition.InsideVertical, stroke).With(BorderPosition.Bottom, stroke);

        var left = _frames.Apply(CellBorders.None, _resolver.Resolve(BorderFrame.Empty, after, _resolver.Place(5, 0, 5, 0, 5, 1)));
        var right = _frames.Apply(CellBorders.None, _resolver.Resolve(BorderFrame.Empty, after, _resolver.Place(5, 1, 5, 0, 5, 1)));

        Assert.True(left.Right.IsVisible);
        Assert.False(left.Left.IsVisible);
        Assert.True(right.Left.IsVisible);
        Assert.False(right.Right.IsVisible);
        Assert.True(left.Bottom.IsVisible && right.Bottom.IsVisible);
    }

    [Fact]
    public void LaPortataDiLedgerHaLeDiagonali()
    {
        Assert.True(_frames.Scope(false, false).Diagonals);
        Assert.True(_frames.Scope(true, false).InsideHorizontal);
        Assert.False(_frames.Scope(true, false).InsideVertical);
    }
}

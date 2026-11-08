using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class TransitionEffectTests
{
    private readonly ITransitionEffectFactory _factory = new TransitionEffectFactory();

    [Theory]
    [InlineData("barWipe", "fadeOverColor", false)]
    [InlineData("barWipe", "leftToRight", false)]
    [InlineData("barWipe", "leftToRight", true)]
    [InlineData("barWipe", "topToBottom", false)]
    [InlineData("barWipe", "topToBottom", true)]
    [InlineData("barnDoorWipe", "horizontal", false)]
    [InlineData("barnDoorWipe", "horizontal", true)]
    [InlineData("barnDoorWipe", "vertical", false)]
    [InlineData("barnDoorWipe", "vertical", true)]
    [InlineData("blindsWipe", "horizontal", false)]
    [InlineData("blindsWipe", "vertical", false)]
    [InlineData("checkerBoardWipe", "across", false)]
    [InlineData("checkerBoardWipe", "down", false)]
    [InlineData("clockWipe", "clockwiseTwelve", false)]
    [InlineData("clockWipe", "clockwiseTwelve", true)]
    [InlineData("dissolve", "default", false)]
    [InlineData("ellipseWipe", "circle", false)]
    [InlineData("ellipseWipe", "circle", true)]
    [InlineData("ellipseWipe", "horizontal", false)]
    [InlineData("ellipseWipe", "vertical", false)]
    [InlineData("fade", "crossfade", false)]
    [InlineData("fade", "fadeOverColor", false)]
    [InlineData("fanWipe", "centerTop", false)]
    [InlineData("fourBoxWipe", "cornersIn", false)]
    [InlineData("fourBoxWipe", "cornersOut", false)]
    [InlineData("irisWipe", "diamond", false)]
    [InlineData("irisWipe", "diamond", true)]
    [InlineData("irisWipe", "rectangle", false)]
    [InlineData("irisWipe", "rectangle", true)]
    [InlineData("miscShapeWipe", "across", false)]
    [InlineData("miscShapeWipe", "bottomCenter", false)]
    [InlineData("miscShapeWipe", "bottomLeft", false)]
    [InlineData("miscShapeWipe", "bottomRight", false)]
    [InlineData("miscShapeWipe", "circle", false)]
    [InlineData("miscShapeWipe", "cornersIn", false)]
    [InlineData("miscShapeWipe", "cornersOut", false)]
    [InlineData("miscShapeWipe", "diamond", false)]
    [InlineData("miscShapeWipe", "fanOutHorizontal", false)]
    [InlineData("miscShapeWipe", "heart", false)]
    [InlineData("miscShapeWipe", "horizontal", false)]
    [InlineData("miscShapeWipe", "leftToRight", false)]
    [InlineData("miscShapeWipe", "rightCenter", false)]
    [InlineData("miscShapeWipe", "topCenter", false)]
    [InlineData("miscShapeWipe", "topLeft", false)]
    [InlineData("miscShapeWipe", "topRight", false)]
    [InlineData("miscShapeWipe", "topToBottom", false)]
    [InlineData("miscShapeWipe", "vertical", false)]
    [InlineData("pinWheelWipe", "eightBlade", false)]
    [InlineData("pinWheelWipe", "fourBlade", false)]
    [InlineData("pinWheelWipe", "oneBlade", false)]
    [InlineData("pinWheelWipe", "threeBlade", false)]
    [InlineData("pinWheelWipe", "twoBladeVertical", false)]
    [InlineData("pushWipe", "combHorizontal", false)]
    [InlineData("pushWipe", "combVertical", false)]
    [InlineData("pushWipe", "fromBottom", false)]
    [InlineData("pushWipe", "fromLeft", false)]
    [InlineData("pushWipe", "fromRight", false)]
    [InlineData("pushWipe", "fromTop", false)]
    [InlineData("randomBarWipe", "horizontal", false)]
    [InlineData("randomBarWipe", "vertical", false)]
    [InlineData("slideWipe", "fromBottom", false)]
    [InlineData("slideWipe", "fromBottom", true)]
    [InlineData("slideWipe", "fromBottomLeft", false)]
    [InlineData("slideWipe", "fromBottomLeft", true)]
    [InlineData("slideWipe", "fromBottomRight", false)]
    [InlineData("slideWipe", "fromBottomRight", true)]
    [InlineData("slideWipe", "fromLeft", false)]
    [InlineData("slideWipe", "fromLeft", true)]
    [InlineData("slideWipe", "fromRight", false)]
    [InlineData("slideWipe", "fromRight", true)]
    [InlineData("slideWipe", "fromTop", false)]
    [InlineData("slideWipe", "fromTop", true)]
    [InlineData("slideWipe", "fromTopLeft", false)]
    [InlineData("slideWipe", "fromTopLeft", true)]
    [InlineData("slideWipe", "fromTopRight", false)]
    [InlineData("slideWipe", "fromTopRight", true)]
    [InlineData("snakeWipe", "topLeftHorizontal", false)]
    [InlineData("snakeWipe", "topLeftHorizontal", true)]
    [InlineData("snakeWipe", "topLeftVertical", false)]
    [InlineData("snakeWipe", "topLeftVertical", true)]
    [InlineData("spiralWipe", "bottomLeftClockwise", true)]
    [InlineData("spiralWipe", "bottomRightCounterClockwise", true)]
    [InlineData("spiralWipe", "topLeftClockwise", false)]
    [InlineData("spiralWipe", "topRightCounterClockwise", false)]
    [InlineData("waterfallWipe", "horizontalLeft", false)]
    [InlineData("waterfallWipe", "horizontalLeft", true)]
    [InlineData("waterfallWipe", "horizontalRight", false)]
    [InlineData("waterfallWipe", "horizontalRight", true)]
    [InlineData("zoom", "rotateIn", false)]
    public void OgniTransizioneDiImpressSiVedeConIlSuoEffetto(string type, string subtype, bool reverse)
    {
        var effect = _factory.Create(new SmilTransition(type, subtype, reverse));

        if (type != "fade") Assert.NotEqual("fade", effect.Family);
        Assert.True(Visible(effect.At(0)) < 0.05, $"{effect.Family} visibile all'inizio");
        Assert.Equal(1, Visible(effect.At(1)), 3);
        var middle = Visible(effect.At(0.6));
        Assert.InRange(middle, 0.01, 1);
    }

    [Fact]
    public void LeTransizioni3DSonoUnApprossimazioneDichiarata()
    {
        Assert.True(_factory.Create(new SmilTransition("miscShapeWipe", "vertical", false)).Approximate);
        Assert.False(_factory.Create(new SmilTransition("barWipe", "leftToRight", false)).Approximate);
    }

    [Fact]
    public void LaTendinaDaSinistraScopreDaSinistra()
    {
        var frame = _factory.Create(new SmilTransition("barWipe", "leftToRight", false)).At(0.25);
        var clip = Assert.IsType<TransitionRectangle>(Assert.Single(frame.Incoming.Clip!));

        Assert.Equal(0, clip.X, 6);
        Assert.Equal(0.25, clip.Width, 6);
    }

    [Fact]
    public void SpingereDaDestraMuoveEntrambeLeDiapositiveVersoSinistra()
    {
        var frame = _factory.Create(new SmilTransition("pushWipe", "fromRight", false)).At(0.25);

        Assert.Equal(0.75, frame.Incoming.OffsetX, 6);
        Assert.Equal(-0.25, frame.Outgoing.OffsetX, 6);
    }

    private double Visible(TransitionFrame frame)
    {
        var incoming = Shown(frame.Incoming);
        if (!frame.OutgoingOnTop) return incoming;
        var outgoing = Shown(frame.Outgoing);
        return Math.Clamp(incoming - outgoing, 0, 1);
    }

    private double Shown(SlideLayerState state)
    {
        var coverage = state.Clip is { } clip ? Math.Min(1, clip.Sum(shape => shape.Area)) : 1;
        var overlap = Math.Max(0, 1 - Math.Abs(state.OffsetX)) * Math.Max(0, 1 - Math.Abs(state.OffsetY));
        var scale = Math.Min(1, Math.Abs(state.ScaleX * state.ScaleY));
        return state.Opacity * coverage * overlap * scale;
    }
}

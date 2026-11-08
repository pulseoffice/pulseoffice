using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class CanvasContextChooserTests
{
    private readonly CanvasContextChooser _chooser = new();

    [Theory]
    [InlineData(CanvasPressTarget.Empty, "canvas")]
    [InlineData(CanvasPressTarget.Element, "object")]
    [InlineData(CanvasPressTarget.Editor, "text")]
    public void InModalitaNormaleIlMenuDipendeDaDoveSiPreme(CanvasPressTarget target, string root) =>
        Assert.Equal(root, _chooser.RootFor(new CanvasPress(target, CanvasMode.Normal)));

    [Theory]
    [InlineData(CanvasPressTarget.Empty)]
    [InlineData(CanvasPressTarget.Element)]
    public void ModificandoIPuntiIlMenuEQuelloDeiPunti(CanvasPressTarget target) =>
        Assert.Equal("points", _chooser.RootFor(new CanvasPress(target, CanvasMode.Points)));

    [Theory]
    [InlineData(CanvasMode.Drawing)]
    [InlineData(CanvasMode.Glue)]
    [InlineData(CanvasMode.Tool)]
    public void NeiModiSpecialiNonSiApreNulla(CanvasMode mode) =>
        Assert.Null(_chooser.RootFor(new CanvasPress(CanvasPressTarget.Element, mode)));
}

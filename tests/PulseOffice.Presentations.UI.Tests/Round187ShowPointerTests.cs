using Avalonia.Input;
using PulseOffice.Presentations.UI;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round187ShowPointerTests
{
    private readonly ShowPointerGestures _gestures = new();

    [Theory]
    [InlineData(PointerUpdateKind.LeftButtonPressed, ShowMove.Forward)]
    [InlineData(PointerUpdateKind.RightButtonPressed, ShowMove.Backward)]
    [InlineData(PointerUpdateKind.MiddleButtonPressed, ShowMove.None)]
    [InlineData(PointerUpdateKind.XButton1Pressed, ShowMove.None)]
    public void OnlyTheLeftButtonAdvancesTheShowAndTheRightOneGoesBack(PointerUpdateKind kind, ShowMove expected) =>
        Assert.Equal(expected, _gestures.ForPress(kind));

    [Theory]
    [InlineData(-1.0, ShowMove.Forward)]
    [InlineData(1.0, ShowMove.Backward)]
    [InlineData(0.0, ShowMove.None)]
    public void TheWheelMovesThroughTheShow(double deltaY, ShowMove expected) =>
        Assert.Equal(expected, _gestures.ForWheel(deltaY));
}

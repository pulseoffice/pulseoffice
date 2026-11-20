using Avalonia;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round281ShapeEditorAreaTests
{
    private static PulseOffice.Testing.UiThreadSession Session => MacroEventsViewTests.Session;

    [Fact]
    public Task The_overlay_editor_covers_the_inner_text_area_of_the_shape() => Session.Dispatch(() =>
    {
        var shape = new ShapeObject { Id = "e", Shape = ShapeKind.Ellipse, Text = "Uno" };
        var editor = new ShapeTextEditor();
        editor.Begin(new ShapeTextEditStart(shape.Id, shape, new ShapeTextPlacement(new Rect(10, 20, 200, 100), 1), new ShapeSurroundings()));

        var inner = editor.Inner(new Rect(10, 20, 200, 100));

        Assert.True(inner.X > 10 && inner.Y > 20);
        Assert.True(inner.Width < 200 && inner.Height < 100);
        Assert.Equal(110, inner.Center.X, 6);
        Assert.Equal(70, inner.Center.Y, 6);
    }, CancellationToken.None);
}

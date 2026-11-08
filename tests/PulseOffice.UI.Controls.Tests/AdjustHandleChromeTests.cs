using Avalonia;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class AdjustHandleChromeTests
{
    private readonly AdjustHandleChrome _chrome = new();
    private readonly ShapeObject _rounded = new() { Id = "r", Shape = ShapeKind.RoundedRectangle };
    private readonly Rect _box = new(100, 50, 200, 100);

    [Fact]
    public void La_maniglia_di_regolazione_sta_sul_bordo_e_si_afferra()
    {
        var frame = new AdjustFrame(_box);
        var point = Assert.Single(_chrome.Points(_rounded, frame));
        Assert.Equal(100 + 16.667, point.X, 1);
        Assert.Equal(50, point.Y, 6);
        Assert.NotNull(_chrome.Grip(_rounded, frame, new Point(point.X + 3, point.Y - 3), 5));
        Assert.Null(_chrome.Grip(_rounded, frame, new Point(point.X + 30, point.Y), 5));
    }

    [Fact]
    public void Trascinare_la_maniglia_cambia_la_regolazione_anche_con_la_forma_ruotata()
    {
        var frame = new AdjustFrame(_box, 90);
        var turned = Assert.Single(_chrome.Points(_rounded, frame));
        Assert.Equal(_box.Center.X + 50, turned.X, 1);
        var grip = _chrome.Grip(_rounded, frame, turned, 5)!;
        var dragged = _chrome.Drag(grip, frame, new Point(_box.Center.X + 50, _box.Center.Y - 100 + 40));
        Assert.True(dragged.Changed);
        var preset = Assert.IsType<PresetGeometry>(dragged.Preview.Computed);
        Assert.Equal(40000, preset.Adjustments.Single().Value, 0);
        Assert.Equal(ShapeKind.RoundedRectangle, dragged.Preview.Shape);
    }

    [Fact]
    public void La_maniglia_segue_il_ribaltamento_della_forma_e_della_cornice()
    {
        var mirrored = _rounded with { FlipHorizontal = true };
        var point = Assert.Single(_chrome.Points(mirrored, new AdjustFrame(_box)));
        Assert.Equal(300 - 16.667, point.X, 1);
        Assert.Equal(point.X, Assert.Single(_chrome.Points(_rounded, new AdjustFrame(_box, 0, FlipHorizontal: true))).X, 6);
        Assert.Equal(300 + 16.667 - 200, Assert.Single(_chrome.Points(mirrored, new AdjustFrame(_box, 0, FlipHorizontal: true))).X, 1);

        var grip = _chrome.Grip(mirrored, new AdjustFrame(_box), point, 5)!;
        var dragged = _chrome.Drag(grip, new AdjustFrame(_box), new Point(300 - 40, 50));
        Assert.Equal(40000, Assert.IsType<PresetGeometry>(dragged.Preview.Computed).Adjustments.Single().Value, 0);
        Assert.Equal(300 - 40, Assert.Single(_chrome.Points(dragged.Preview, new AdjustFrame(_box))).X, 1);
    }

    [Fact]
    public void Il_ribaltamento_verticale_sposta_la_maniglia_sul_bordo_opposto()
    {
        var point = Assert.Single(_chrome.Points(_rounded with { FlipVertical = true }, new AdjustFrame(_box)));
        Assert.Equal(150, point.Y, 6);
        Assert.Equal(100 + 16.667, point.X, 1);
    }
}

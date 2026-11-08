using Avalonia;
using Avalonia.Media;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ObjectFrameChromeTests
{
    private readonly ObjectManipulator _manipulator = new();
    private readonly FrameStyles _styles = new();
    private readonly ObjectBox _box = new(100, 50, 200, 100);

    [Fact]
    public void Le_due_varianti_sono_dati_diversi_sulla_stessa_geometria()
    {
        Assert.Equal(FrameOutline.Solid, _styles.Filled.Outline);
        Assert.Equal(FrameHandleFill.Accent, _styles.Filled.Handles);
        Assert.Equal(FrameRotationMark.Square, _styles.Filled.Rotation);
        Assert.Equal(FrameOutline.Dashed, _styles.Dashed.Outline);
        Assert.Equal(FrameHandleFill.Hollow, _styles.Dashed.Handles);
        Assert.Equal(FrameRotationMark.Circle, _styles.Dashed.Rotation);

        var scene = new FrameScene(_box, 0, Brushes.Blue);
        var filled = new ObjectFrameChrome(_manipulator, _styles.Filled).Handles(scene);
        var dashed = new ObjectFrameChrome(_manipulator, _styles.Dashed).Handles(scene);
        Assert.Equal(filled, dashed);
        Assert.Equal(9, filled.Count);
        Assert.Equal(new Point(200, 50 - _manipulator.RotationDistance), filled.Single(entry => entry.Handle == ObjectHandle.Rotate).Area.Center);
    }

    [Fact]
    public void Le_maniglie_restano_della_stessa_misura_a_schermo_con_qualunque_scala()
    {
        var chrome = new ObjectFrameChrome(_manipulator, _styles.Dashed);
        var half = chrome.Handles(new FrameScene(_box, 0, Brushes.Blue, 0.5));
        var corner = half.Single(entry => entry.Handle == ObjectHandle.BottomRight).Area;
        Assert.Equal(_manipulator.HandleSize / 2, corner.Width, 6);
        Assert.Equal(new Point(300, 150), corner.Center);
        Assert.Equal(50 - _manipulator.RotationDistance / 2, half.Single(entry => entry.Handle == ObjectHandle.Rotate).Area.Center.Y, 6);
        Assert.Equal(ObjectHandle.Rotate, chrome.HandleAt(new FrameScene(_box, 0, Brushes.Blue, 0.5), new Point(200, 50 - _manipulator.RotationDistance / 2)));
        Assert.Equal(ObjectHandle.None, chrome.HandleAt(new FrameScene(_box, 0, Brushes.Blue, 0.5), new Point(200, 50 - _manipulator.RotationDistance)));
    }

    [Fact]
    public void La_presa_segue_la_rotazione_attorno_al_centro()
    {
        var chrome = new ObjectFrameChrome(_manipulator, _styles.Filled);
        var turned = new FrameScene(_box, 90, Brushes.Blue);
        Assert.Equal(ObjectHandle.Rotate, chrome.HandleAt(turned, new Point(200 + 50 + _manipulator.RotationDistance, 100)));
        Assert.Equal(ObjectHandle.Top, chrome.HandleAt(turned, new Point(250, 100)));
        Assert.Equal(ObjectHandle.TopRight, chrome.HandleAt(turned, new Point(250, 200)));
        Assert.Equal(ObjectHandle.Body, chrome.HandleAt(turned, new Point(200, 100)));
        Assert.Equal(ObjectHandle.None, chrome.HandleAt(turned, new Point(110, 60)));
    }

    [Fact]
    public void Senza_modifica_non_ci_sono_maniglie()
    {
        var chrome = new ObjectFrameChrome(_manipulator, _styles.Dashed);
        Assert.Empty(chrome.Handles(new FrameScene(_box, 0, Brushes.Blue, 1, Editable: false)));
    }
}

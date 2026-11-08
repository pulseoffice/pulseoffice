using PulseOffice.Document.Borders;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Borders;
using PulseOffice.UI.Controls.Fills;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditBorderFillTests
{
    [Fact]
    public void Lo_spessore_non_finito_non_entra_nel_bordo()
    {
        var model = new BorderEditorModel(new BorderFrameScope(false, false, false), BorderFrame.Empty, new BorderPresetCatalog(), new BorderFrameEditing());

        model.SetWidth(double.NaN);

        Assert.True(double.IsFinite(model.Stroke.WidthPoints));
    }

    [Fact]
    public void I_valori_non_finiti_non_entrano_nel_riempimento()
    {
        var model = new FillEditorModel(new ShapeFill { Kind = ShapeFillKind.Gradient }, new FillEditorOptions([FillPage.Solid, FillPage.Gradient, FillPage.Hatch]), new GradientStopEditing(), new FillPatternCatalog());

        model.SetTransparency(double.NaN);
        model.SetGradientAngle(double.PositiveInfinity);
        model.MoveStop(double.NaN);
        model.SetHatchDistance(double.NaN);

        Assert.True(double.IsFinite(model.Fill.Opacity));
        Assert.True(double.IsFinite(model.Fill.Gradient.AngleDegrees));
        Assert.All(model.Fill.Gradient.Stops, stop => Assert.True(double.IsFinite(stop.Position)));
        Assert.True(double.IsFinite(model.Fill.Hatch.Distance.ToPoints()));
    }
}

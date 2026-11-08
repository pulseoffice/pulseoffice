using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditShapesTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    [Fact]
    public Task Una_forma_estrusa_piu_piccola_di_un_punto_si_disegna_senza_eccezioni() => Session.Dispatch(() =>
    {
        var shape = new ShapeObject
        {
            Id = "s",
            Shape = ShapeKind.Rectangle,
            Style = ShapeStyle.Default with { Extrusion = new ShapeExtrusion { Kind = ExtrusionKind.Extrude, Depth = Twip.FromPoints(10) } },
        };
        var recorded = new Avalonia.Media.DrawingGroup();
        using (var context = recorded.Open())
            new ShapeRenderer().Render(context, shape, new Rect(0, 0, 0.5, 0.25));

        Assert.NotEmpty(recorded.Children);
    }, CancellationToken.None);

    [Fact]
    public void Il_testo_cambiato_nel_dialogo_di_stile_sostituisce_il_testo_formattato()
    {
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Text = "vecchio", Runs = [new ShapeTextRun("vecchio")] };
        var styling = new ShapeStyling(new ShapeGeometry());
        var editable = styling.Editable(shape)!;

        var renamed = (ShapeObject)styling.Apply(shape, editable with { Text = "nuovo" })!;
        var restyled = (ShapeObject)styling.Apply(shape, editable)!;

        Assert.Equal("nuovo", renamed.Text);
        Assert.Null(renamed.Runs);
        Assert.Same(shape.Runs, restyled.Runs);
    }
}

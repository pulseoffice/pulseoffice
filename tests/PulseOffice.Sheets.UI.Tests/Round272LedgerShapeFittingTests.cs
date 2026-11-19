using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.UI.ShapeText;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round272LedgerShapeFittingTests
{
    private static readonly PulseOffice.Testing.UiThreadSession Session = PulseOffice.Testing.UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    private static PlacedObject Placed(TextAutofit fit) => new()
    {
        Id = "o",
        Anchor = new CellAddress(0, 0),
        Size = new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(1)),
        Content = new ShapeObject
        {
            Id = "s",
            Shape = ShapeKind.Rectangle,
            Text = "Uno\nDue\nTre\nQuattro",
            Style = ShapeStyle.Default with { Text = ShapeTextStyle.Default with { Autofit = fit, Size = 18 } },
        },
    };

    [Fact]
    public Task A_Ledger_shape_that_resizes_to_its_text_grows_and_a_fixed_one_does_not() => Session.Dispatch(() =>
    {
        var fitting = new LedgerShapeFitting();

        Assert.True(fitting.Fit(Placed(TextAutofit.ResizeShape)).Size.Height.Value > Placed(TextAutofit.None).Size.Height.Value);
        Assert.Equal(Placed(TextAutofit.None), fitting.Fit(Placed(TextAutofit.None)));
    }, CancellationToken.None);
}

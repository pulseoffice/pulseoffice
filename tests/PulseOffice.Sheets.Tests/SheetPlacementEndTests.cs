using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class SheetPlacementEndTests
{
    private readonly ISheetPlacement _placement = new SheetPlacement();

    private static PlacedObject Placed(int width, int height, int offsetX = 0, int offsetY = 0) => new()
    {
        Id = "x",
        Content = new ShapeObject { Id = "x", Shape = ShapeKind.Rectangle },
        Anchor = new CellAddress(1, 1),
        OffsetX = new Twip(offsetX),
        OffsetY = new Twip(offsetY),
        Size = new DocumentSize(new Twip(width), new Twip(height)),
    };

    [Fact]
    public void LaFineSegueLargheAltezzeVereNascosteEAutomatiche()
    {
        var layout = new SheetLayout { DefaultColumnWidth = new Twip(1000), DefaultRowHeight = new Twip(300) };
        layout.SetColumnWidth(2, new Twip(500));
        layout.SetAutomaticRowHeight(2, new Twip(600));
        layout.HideRow(3);

        var end = _placement.End(layout, Placed(1700, 1000, 100, 50));

        Assert.Equal(new CellAddress(4, 3), end.Cell);
        Assert.Equal(300, end.X.Value);
        Assert.Equal(150, end.Y.Value);
    }

    [Fact]
    public void UnaFineSulBordoCadeNellaCellaSuccessivaConScostamentoZero()
    {
        var layout = new SheetLayout { DefaultColumnWidth = new Twip(1000), DefaultRowHeight = new Twip(300) };

        var end = _placement.End(layout, Placed(2000, 300));

        Assert.Equal(new CellAddress(2, 3), end.Cell);
        Assert.Equal(0, end.X.Value);
        Assert.Equal(0, end.Y.Value);
    }
}

using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Exchange.Tests;

public sealed class DeepAuditExchangeTests
{
    [Fact]
    public void UnOggettoConDimensioniNegativeArrivaConLaSuaMisuraNaturale()
    {
        var serializer = new JsonExchangeSerializer(new LoadedExchangeTypeCatalog(), new ExchangeContentGuard());
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Ellipse };
        var package = new ExchangePackage
        {
            Id = "p",
            Content = new ObjectsContent([new ExchangedObject(shape, new DocumentSize(new Twip(-500), new Twip(-800)))]),
        };

        var read = serializer.Read(serializer.Write(package));

        var item = Assert.Single(Assert.IsType<ObjectsContent>(read?.Content).Objects);
        Assert.Equal(shape.NaturalSize, item.Size);
    }
}

public sealed class DeepAuditRoundNineExchangeTests
{
    [Fact]
    public void UnOggettoSmisuratoArrivaConDimensioniEntroIlTetto()
    {
        var serializer = new JsonExchangeSerializer(new LoadedExchangeTypeCatalog(), new ExchangeContentGuard());
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Ellipse };
        var package = new ExchangePackage
        {
            Id = "p",
            Content = new ObjectsContent([new ExchangedObject(shape, new DocumentSize(new Twip(int.MaxValue), new Twip(int.MaxValue)))]),
        };

        var read = serializer.Read(serializer.Write(package));

        var item = Assert.Single(Assert.IsType<ObjectsContent>(read?.Content).Objects);
        Assert.InRange(item.Size.Width.Value, 1, ExchangeContentGuard.MaximumExtent);
        Assert.InRange(item.Size.Height.Value, 1, ExchangeContentGuard.MaximumExtent);
    }

    [Fact]
    public void UnColoreDiCellaConCaratteriNonEsadecimaliNonEntraNellHtml()
    {
        var grid = new CellRangeContent
        {
            SheetName = "S",
            RowCount = 1,
            ColumnCount = 1,
            Rows = [[new ExchangeCell { Kind = ExchangeCellKind.Text, Text = "a", ColorHex = "\"><b>xy" }]],
        };

        var html = new CellGridConversions().Html(grid);

        Assert.DoesNotContain("\"><b>", html, StringComparison.Ordinal);
    }
}

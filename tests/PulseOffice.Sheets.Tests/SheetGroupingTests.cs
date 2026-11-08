using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class SheetGroupingTests
{
    private static readonly ISheetGrouping Grouping = new SheetGrouping();

    private static PlacedObject Shape(string id, int row, int column, double widthCm, double heightCm) =>
        new()
        {
            Id = id,
            Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
            Anchor = new CellAddress(row, column),
            Size = new DocumentSize(Twip.FromCentimeters(widthCm), Twip.FromCentimeters(heightCm)),
        };

    private static Worksheet SheetWith(params PlacedObject[] objects)
    {
        var sheet = new Worksheet("Foglio1");
        var order = 0;

        foreach (var placed in objects) sheet.Objects.Add(placed with { ZOrder = order++ });

        return sheet;
    }

    [Fact]
    public void UnOggettoSoloNonFaUnGruppo()
    {
        var sheet = SheetWith(Shape("a", 0, 0, 2, 2));

        Assert.Null(Grouping.Group(sheet, [.. sheet.Objects], "g1"));
    }

    [Fact]
    public void IlGruppoAbbracciaTuttiIFigli()
    {
        var sheet = SheetWith(Shape("a", 0, 0, 2, 2), Shape("b", 6, 3, 2, 2));

        var group = Grouping.Group(sheet, [.. sheet.Objects], "g1")!;

        Assert.Equal(new CellAddress(0, 0), group.Anchor);
        Assert.True(group.Size.Width.ToCentimeters() > 2);
        Assert.True(group.Size.Height.ToCentimeters() > 2);
    }

    [Fact]
    public void IFigliSiTengonoLeProporzioni()
    {
        var sheet = SheetWith(Shape("a", 0, 0, 2, 2), Shape("b", 6, 3, 2, 2));

        var group = Grouping.Group(sheet, [.. sheet.Objects], "g1")!;
        var children = Assert.IsType<ShapeGroupObject>(group.Content).Children;

        Assert.Equal(2, children.Count);
        Assert.Equal(0, children[0].X, 6);
        Assert.Equal(0, children[0].Y, 6);
        Assert.All(children, child => Assert.InRange(child.X + child.Width, 0, 1.0001));
        Assert.All(children, child => Assert.InRange(child.Y + child.Height, 0, 1.0001));
    }

    [Fact]
    public void IFigliRestanoNellOrdineDiDisegno()
    {
        var sheet = SheetWith(Shape("a", 0, 0, 2, 2), Shape("b", 2, 2, 2, 2));
        sheet.Objects[0] = sheet.Objects[0] with { ZOrder = 9 };

        var group = Grouping.Group(sheet, [.. sheet.Objects], "g1")!;
        var children = Assert.IsType<ShapeGroupObject>(group.Content).Children;

        Assert.Equal("b", children[0].Content.Id);
        Assert.Equal("a", children[1].Content.Id);
    }

    [Fact]
    public void SeparareRestituisceIFigli()
    {
        var sheet = SheetWith(Shape("a", 0, 0, 2, 2), Shape("b", 6, 3, 3, 1));

        var group = Grouping.Group(sheet, [.. sheet.Objects], "g1")!;
        var released = Grouping.Ungroup(sheet, group)!;

        Assert.Equal(["a", "b"], released.Select(placed => placed.Id));
    }

    [Fact]
    public void IlGiroCompletoConservaPosizioneEMisura()
    {
        var sheet = SheetWith(Shape("a", 0, 0, 2, 2), Shape("b", 6, 3, 3, 1));

        var group = Grouping.Group(sheet, [.. sheet.Objects], "g1")!;
        var released = Grouping.Ungroup(sheet, group)!;

        var placement = new SheetPlacement();
        var origin = CellAddress.First;

        foreach (var before in sheet.Objects)
        {
            var after = released.First(placed => placed.Id == before.Id);

            var first = placement.Box(sheet.Layout, origin, before);
            var second = placement.Box(sheet.Layout, origin, after);

            Assert.Equal(first.Left.ToCentimeters(), second.Left.ToCentimeters(), 1);
            Assert.Equal(first.Top.ToCentimeters(), second.Top.ToCentimeters(), 1);
            Assert.Equal(first.Width.ToCentimeters(), second.Width.ToCentimeters(), 1);
            Assert.Equal(first.Height.ToCentimeters(), second.Height.ToCentimeters(), 1);
        }
    }

    [Fact]
    public void IngrandireIlGruppoIngrandisceIFigli()
    {
        var sheet = SheetWith(Shape("a", 0, 0, 2, 2), Shape("b", 6, 3, 2, 2));

        var group = Grouping.Group(sheet, [.. sheet.Objects], "g1")!;

        var doubled = group with
        {
            Size = new DocumentSize(
                new Twip(group.Size.Width.Value * 2), new Twip(group.Size.Height.Value * 2)),
        };

        var released = Grouping.Ungroup(sheet, doubled)!;

        Assert.Equal(4, released[0].Size.Width.ToCentimeters(), 1);
    }

    [Fact]
    public void SeparareQualcosaCheNonEUnGruppoNonFaNulla()
    {
        var sheet = SheetWith(Shape("a", 0, 0, 2, 2));

        Assert.Null(Grouping.Ungroup(sheet, sheet.Objects[0]));
    }
}

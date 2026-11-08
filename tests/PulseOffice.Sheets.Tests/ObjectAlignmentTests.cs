using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class ObjectAlignmentTests
{
    private static readonly IObjectAlignment Alignment = new ObjectAlignment();
    private static readonly ISheetPlacement Placement = new SheetPlacement();

    private static Worksheet Sheet => new("Foglio1");

    private static PlacedObject Shape(string id, int row, int column, double widthCm, double heightCm) =>
        new()
        {
            Id = id,
            Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
            Anchor = new CellAddress(row, column),
            Size = new DocumentSize(Twip.FromCentimeters(widthCm), Twip.FromCentimeters(heightCm)),
        };

    private static PlacementBox Box(Worksheet sheet, PlacedObject placed) =>
        Placement.Box(sheet.Layout, CellAddress.First, placed);

    [Fact]
    public void ConUnOggettoSoloNonSiAllineaNulla()
    {
        Assert.Empty(Alignment.Align(Sheet, [Shape("a", 0, 0, 2, 2)], ObjectAlignmentEdge.Left));
    }

    [Fact]
    public void AllineareASinistraPortaTuttiAlBordoPiuASinistra()
    {
        var sheet = Sheet;
        var objects = new[] { Shape("a", 0, 1, 2, 2), Shape("b", 4, 5, 3, 1) };

        var moved = Alignment.Align(sheet, objects, ObjectAlignmentEdge.Left);
        var lefts = moved.Select(placed => Box(sheet, placed).Left.Value).Distinct();

        Assert.Single(lefts);
    }

    [Fact]
    public void AllineareADestraAllineaIBordiDestri()
    {
        var sheet = Sheet;
        var objects = new[] { Shape("a", 0, 1, 2, 2), Shape("b", 4, 5, 3, 1) };

        var moved = Alignment.Align(sheet, objects, ObjectAlignmentEdge.Right);
        var rights = moved.Select(placed => Box(sheet, placed).Right.Value).Distinct();

        Assert.Single(rights);
    }

    [Fact]
    public void AllineareAlCentroAllineaICentri()
    {
        var sheet = Sheet;
        var objects = new[] { Shape("a", 0, 1, 2, 2), Shape("b", 4, 5, 4, 1) };

        var moved = Alignment.Align(sheet, objects, ObjectAlignmentEdge.HorizontalCentre);

        var centres = moved
            .Select(placed => Box(sheet, placed))
            .Select(box => box.Left.Value + box.Width.Value / 2)
            .ToList();

        Assert.True(Math.Abs(centres[0] - centres[1]) <= 40);
    }

    [Fact]
    public void AllineareInAltoAllineaIBordiSuperiori()
    {
        var sheet = Sheet;
        var objects = new[] { Shape("a", 2, 0, 2, 2), Shape("b", 7, 3, 2, 3) };

        var moved = Alignment.Align(sheet, objects, ObjectAlignmentEdge.Top);
        var tops = moved.Select(placed => Box(sheet, placed).Top.Value).Distinct();

        Assert.Single(tops);
    }

    [Fact]
    public void LAllineamentoNonCambiaLeMisure()
    {
        var sheet = Sheet;
        var objects = new[] { Shape("a", 0, 1, 2, 2), Shape("b", 4, 5, 3, 1) };

        var moved = Alignment.Align(sheet, objects, ObjectAlignmentEdge.Left);

        Assert.Equal(2, moved[0].Size.Width.ToCentimeters(), 1);
        Assert.Equal(3, moved[1].Size.Width.ToCentimeters(), 1);
    }

    [Fact]
    public void ConDueOggettiNonSiDistribuisce()
    {
        Assert.Empty(Alignment.Distribute(
            Sheet, [Shape("a", 0, 0, 2, 2), Shape("b", 0, 4, 2, 2)], ObjectDistribution.Horizontal));
    }

    [Fact]
    public void DistribuireLasciaSpaziUguali()
    {
        var sheet = Sheet;

        var objects = new[]
        {
            Shape("a", 0, 0, 2, 2),
            Shape("b", 0, 2, 1, 2),
            Shape("c", 0, 9, 2, 2),
        };

        var moved = Alignment.Distribute(sheet, objects, ObjectDistribution.Horizontal);

        var boxes = moved.Select(placed => Box(sheet, placed)).OrderBy(box => box.Left.Value).ToList();

        var firstGap = boxes[1].Left.Value - boxes[0].Right.Value;
        var secondGap = boxes[2].Left.Value - boxes[1].Right.Value;

        Assert.True(Math.Abs(firstGap - secondGap) <= 40);
    }

    [Fact]
    public void DistribuireNonSpostaIlPrimoEUltimo()
    {
        var sheet = Sheet;

        var objects = new[]
        {
            Shape("a", 0, 0, 2, 2),
            Shape("b", 0, 2, 1, 2),
            Shape("c", 0, 9, 2, 2),
        };

        var before = objects.Select(placed => Box(sheet, placed)).ToList();
        var moved = Alignment.Distribute(sheet, objects, ObjectDistribution.Horizontal)
            .Select(placed => Box(sheet, placed))
            .OrderBy(box => box.Left.Value)
            .ToList();

        Assert.True(Math.Abs(before[0].Left.Value - moved[0].Left.Value) <= 40);
        Assert.True(Math.Abs(before[2].Right.Value - moved[2].Right.Value) <= 40);
    }

    [Fact]
    public void DistribuireConUnOggettoLargoInMezzoNonSuperaIlBordoPiuLontano()
    {
        var sheet = Sheet;

        var objects = new[]
        {
            Shape("a", 0, 0, 1, 2),
            Shape("b", 0, 1, 30, 2),
            Shape("c", 0, 2, 1, 2),
        };

        var farthest = objects.Select(placed => Box(sheet, placed)).Max(box => box.Right.Value);
        var moved = Alignment.Distribute(sheet, objects, ObjectDistribution.Horizontal)
            .Select(placed => Box(sheet, placed))
            .ToList();

        Assert.True(Math.Abs(moved.Max(box => box.Right.Value) - farthest) <= 40);
    }
}

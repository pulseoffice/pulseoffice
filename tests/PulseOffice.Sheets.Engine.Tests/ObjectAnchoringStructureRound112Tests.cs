using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ObjectAnchoringStructureRound112Tests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private readonly ISheetStructure _structure = new SheetStructure();

    private (Worksheet Sheet, CalculationEngine Engine) Sheet(ObjectAnchoring anchoring)
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.Objects.Add(new PlacedObject
        {
            Id = "s1",
            Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Rectangle },
            Anchor = new CellAddress(2, 0),
            Size = new DocumentSize(Twip.FromPoints(60), sheet.Layout.DefaultRowHeight * 3),
            Anchoring = anchoring,
        });
        return (sheet, engine);
    }

    private int Rows(Worksheet sheet, int count) => (sheet.Layout.DefaultRowHeight * count).Value;

    [Fact]
    public void Rows_inserted_inside_a_move_and_size_object_stretch_it()
    {
        var (sheet, engine) = Sheet(ObjectAnchoring.MoveAndSize);

        _structure.InsertRows(engine, 0, 3, 2);

        Assert.Equal(new CellAddress(2, 0), sheet.Objects[0].Anchor);
        Assert.Equal(Rows(sheet, 5), sheet.Objects[0].Size.Height.Value);
    }

    [Fact]
    public void Rows_inserted_below_a_move_and_size_object_leave_it_alone()
    {
        var (sheet, engine) = Sheet(ObjectAnchoring.MoveAndSize);

        _structure.InsertRows(engine, 0, 5, 2);

        Assert.Equal(Rows(sheet, 3), sheet.Objects[0].Size.Height.Value);
    }

    [Fact]
    public void Rows_deleted_inside_a_move_and_size_object_shrink_it()
    {
        var (sheet, engine) = Sheet(ObjectAnchoring.MoveAndSize);

        _structure.DeleteRows(engine, 0, 3, 1);

        Assert.Equal(Rows(sheet, 2), sheet.Objects[0].Size.Height.Value);
    }

    [Fact]
    public void Deleting_the_first_rows_of_a_move_and_size_object_keeps_the_rest()
    {
        var (sheet, engine) = Sheet(ObjectAnchoring.MoveAndSize);

        _structure.DeleteRows(engine, 0, 1, 2);

        var placed = Assert.Single(sheet.Objects);
        Assert.Equal(new CellAddress(1, 0), placed.Anchor);
        Assert.Equal(Rows(sheet, 2), placed.Size.Height.Value);
    }

    [Fact]
    public void Rows_inserted_inside_a_move_object_only_move_it_when_above()
    {
        var (sheet, engine) = Sheet(ObjectAnchoring.Move);

        _structure.InsertRows(engine, 0, 0, 2);

        Assert.Equal(new CellAddress(4, 0), sheet.Objects[0].Anchor);
        Assert.Equal(Rows(sheet, 3), sheet.Objects[0].Size.Height.Value);
    }

    [Fact]
    public void Fixed_objects_neither_move_nor_go_away()
    {
        var (sheet, engine) = Sheet(ObjectAnchoring.Fixed);
        var top = sheet.Layout.RowOffset(2).Value;

        _structure.InsertRows(engine, 0, 0, 3);
        _structure.DeleteRows(engine, 0, 0, 6);

        var placed = Assert.Single(sheet.Objects);
        Assert.Equal(top, sheet.Layout.RowOffset(placed.Anchor.Row).Value + placed.OffsetY.Value);
        Assert.Equal(Rows(sheet, 3), placed.Size.Height.Value);
    }

    [Fact]
    public void Inserted_rows_copy_a_row_height_without_resizing_the_object_twice()
    {
        var (sheet, engine) = Sheet(ObjectAnchoring.MoveAndSize);
        sheet.Layout.SetRowHeight(2, sheet.Layout.DefaultRowHeight * 2);
        var before = sheet.Objects[0].Size.Height.Value;

        _structure.InsertRows(engine, 0, 3, 1);

        Assert.Equal(before + sheet.Layout.RowHeight(3).Value, sheet.Objects[0].Size.Height.Value);
    }
}

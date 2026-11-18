using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round159PrintStructureTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellRange R(int top, int left, int bottom, int right) => new(new CellAddress(top, left), new CellAddress(bottom, right));

    [Fact]
    public void Inserting_rows_inside_the_repeated_rows_grows_them()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.RepeatedPrintRows = 2;
        sheet.RepeatedPrintColumns = 1;

        new SheetStructure().InsertRows(engine, 0, 1, 3);
        new SheetStructure().InsertColumns(engine, 0, 4, 2);

        Assert.Equal(5, sheet.RepeatedPrintRows);
        Assert.Equal(1, sheet.RepeatedPrintColumns);
    }

    [Fact]
    public void Deleting_rows_inside_the_repeated_rows_shrinks_them()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.RepeatedPrintColumns = 3;

        new SheetStructure().DeleteColumns(engine, 0, 2, 4);

        Assert.Equal(2, sheet.RepeatedPrintColumns);
    }

    [Fact]
    public void Moving_a_range_carries_the_print_ranges_inside_it()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.PrintRanges.Add(R(0, 0, 3, 2));
        sheet.PrintRanges.Add(R(20, 0, 21, 1));

        new SheetRangeMove(new SheetStructure()).Move(engine, new RangeMove(sheet.Name, R(0, 0, 4, 2), sheet.Name, new CellAddress(10, 4)));

        Assert.Equal([R(10, 4, 13, 6), R(20, 0, 21, 1)], sheet.PrintRanges);
    }

    [Fact]
    public void Inserting_rows_moves_the_links_of_grouped_shapes()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var summary = workbook.AddSheet("Summary");
        var inner = new ShapeGroupObject
        {
            Id = "inner",
            Children = [new GroupChild(new ShapeObject { Id = "b", Shape = ShapeKind.Rectangle }, 0, 0, 1, 1) { Hyperlink = "#Sheet1.A6" }],
        };
        summary.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = new CellAddress(0, 7),
            Content = new ShapeGroupObject
            {
                Id = "g",
                Children =
                [
                    new GroupChild(new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle }, 0, 0, 0.5, 1) { Hyperlink = "#Sheet1.A5" },
                    new GroupChild(inner, 0.5, 0, 0.5, 1),
                ],
            },
        });

        new SheetStructure().InsertRows(engine, 0, 0, 2);

        var group = Assert.IsType<ShapeGroupObject>(summary.Objects[0].Content);
        Assert.Equal("#Sheet1.A7", group.Children[0].Hyperlink);
        Assert.Equal("#Sheet1.A8", Assert.IsType<ShapeGroupObject>(group.Children[1].Content).Children[0].Hyperlink);
    }
}

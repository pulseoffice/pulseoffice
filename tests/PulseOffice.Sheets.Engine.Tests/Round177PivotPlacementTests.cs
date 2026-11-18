using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Pivot;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round177PivotPlacementTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellRange R(int top, int left, int bottom, int right) => new(new CellAddress(top, left), new CellAddress(bottom, right));

    private static CellAddress A(int row, int column) => new(row, column);

    private static PivotDefinition Pivot(string sheet) => new()
    {
        Id = "p", Name = "p", SourceSheet = sheet, SourceRange = R(0, 0, 3, 1),
        DestinationSheet = sheet, Destination = A(0, 4), OutputRange = R(0, 4, 3, 5),
    };

    [Fact]
    public void Inserting_rows_above_a_pivot_shifts_its_output_range_with_its_destination()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Pivots.Add(Pivot(workbook.Sheets[0].Name));

        new SheetStructure().InsertRows(engine, 0, 0, 2);

        Assert.Equal(A(2, 4), workbook.Pivots[0].Destination);
        Assert.Equal(R(2, 4, 5, 5), workbook.Pivots[0].OutputRange);
    }

    [Fact]
    public void Moving_the_cells_of_a_pivot_moves_its_destination_and_output()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var name = workbook.Sheets[0].Name;
        workbook.Pivots.Add(Pivot(name));

        new SheetRangeMove(new SheetStructure()).Move(engine, new RangeMove(name, R(0, 4, 3, 5), name, A(10, 8)));

        Assert.Equal(A(10, 8), workbook.Pivots[0].Destination);
        Assert.Equal(R(10, 8, 13, 9), workbook.Pivots[0].OutputRange);
        Assert.Equal(R(0, 0, 3, 1), workbook.Pivots[0].SourceRange);
    }
}

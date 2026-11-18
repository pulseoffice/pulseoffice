using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round161PrintTitleStructureTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void Inserting_rows_above_the_print_titles_moves_them_down()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.PrintTitleRows = new PrintTitleBand(2, 2);

        new SheetStructure().InsertRows(engine, 0, 0, 3);

        Assert.Equal(new PrintTitleBand(5, 2), sheet.PrintTitleRows);
    }

    [Fact]
    public void Deleting_columns_across_the_start_of_the_print_titles_moves_and_shrinks_them()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.PrintTitleColumns = new PrintTitleBand(3, 3);

        new SheetStructure().DeleteColumns(engine, 0, 1, 3);

        Assert.Equal(new PrintTitleBand(1, 2), sheet.PrintTitleColumns);
    }
}

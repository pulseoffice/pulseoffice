using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round309InvalidDataTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void Only_values_breaking_their_rule_are_found_up_to_the_limit()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.Validations.Add(new DataValidation
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 0)),
            Kind = ValidationKind.WholeNumber,
            FirstFormula = "1",
            SecondFormula = "10",
        });
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Number(5));
        engine.SetValue(new SheetCell(0, new CellAddress(1, 0)), CellValue.Number(50));
        engine.SetValue(new SheetCell(0, new CellAddress(2, 0)), CellValue.Number(70));
        engine.SetValue(new SheetCell(0, new CellAddress(0, 1)), CellValue.Number(99));

        var finder = new InvalidDataFinder(new DataValidator());

        Assert.Equal(new[] { new CellAddress(1, 0), new CellAddress(2, 0) }, finder.Find(engine, 0, 255));
        Assert.Single(finder.Find(engine, 0, 1));
    }
}

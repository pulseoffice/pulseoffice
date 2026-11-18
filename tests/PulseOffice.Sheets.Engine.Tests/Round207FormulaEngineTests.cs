using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round207FormulaEngineTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    [Fact]
    public void Xlookup_with_an_array_lookup_value_and_several_return_columns_keeps_the_first_column()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("A1"), "=XLOOKUP({2|3};{1|2|3};{10;11|20;21|30;31})");

        Assert.Equal(20, engine.GetValue(C("A1")).AsNumber());
        Assert.Equal(30, engine.GetValue(C("A2")).AsNumber());
        Assert.True(engine.GetValue(C("B1")).IsEmpty);
    }

    [Fact]
    public void A_spill_reference_to_a_single_value_formula_is_a_reference_error()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("A1"), "=1+1");
        engine.SetFormula(C("B1"), "=A1#");

        Assert.Equal(CellError.Reference, engine.GetValue(C("B1")).Error);
    }

    [Fact]
    public void The_engine_reports_every_completed_recalculation_once()
    {
        var (_, engine) = fixture.CreateWorkbook();
        var completed = 0;
        engine.Recalculated += (_, _) => completed++;

        engine.SetFormula(C("A1"), "=B1");
        engine.SetFormula(C("B1"), "=A1");

        Assert.Equal(2, completed);
        Assert.Equal("Sheet1!A1", new CircularReferenceReport().FirstCell(engine)?.Replace(engine.Workbook.Sheets[0].Name, "Sheet1", StringComparison.Ordinal));
    }
}

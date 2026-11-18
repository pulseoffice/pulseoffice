using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round202FormulaEngineTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    [Fact]
    public void Xlookup_with_an_array_lookup_value_spills_one_result_per_element()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("A1"), "=XLOOKUP({2|3|7};{1|2|3};{10|20|30};-1)");

        Assert.Equal(20, engine.GetValue(C("A1")).AsNumber());
        Assert.Equal(30, engine.GetValue(C("A2")).AsNumber());
        Assert.Equal(-1, engine.GetValue(C("A3")).AsNumber());
    }

    [Fact]
    public void Xmatch_with_an_array_lookup_value_spills_one_position_per_element()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("A1"), "=XMATCH({3|1};{1|2|3})");

        Assert.Equal(3, engine.GetValue(C("A1")).AsNumber());
        Assert.Equal(1, engine.GetValue(C("A2")).AsNumber());
    }

    [Fact]
    public void A_spill_and_its_spill_reference_follow_an_inserted_row()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("A1"), "=SEQUENCE(3)");
        engine.SetFormula(C("B1"), "=SUM(A1#)");

        new SheetStructure().InsertRows(engine, 0, 0, 1);

        Assert.Equal("=SUM(A2#)", workbook.Sheets[0][new CellAddress(1, 1)].Formula);
        Assert.Equal(new CellRange(new CellAddress(1, 0), new CellAddress(3, 0)), engine.SpillRanges[C("A2")]);
        Assert.Equal(6, engine.GetValue(C("B2")).AsNumber());
    }

    [Fact]
    public void A_blocked_spill_stays_marked_as_a_dynamic_array_formula()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        engine.SetValue(C("A3"), CellValue.Number(9));
        engine.SetFormula(C("A1"), "=SEQUENCE(3)");

        Assert.Equal(CellError.Spill, engine.GetValue(C("A1")).Error);
        Assert.True(workbook.Sheets[0][new CellAddress(0, 0)].UsesArrays);
    }

    [Fact]
    public void The_circular_reference_report_names_the_first_cell_and_clears_once_resolved()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var report = new CircularReferenceReport();
        engine.SetFormula(C("B2"), "=A1+1");
        engine.SetFormula(C("A1"), "=B2+1");

        Assert.Equal($"{workbook.Sheets[0].Name}!A1", report.FirstCell(engine));

        engine.SetValue(C("A1"), CellValue.Number(1));
        Assert.Null(report.FirstCell(engine));
    }

    [Fact]
    public void A_spill_reference_to_a_blocked_anchor_gives_spill_and_recovers_when_unblocked()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(C("A2"), CellValue.Number(9));
        engine.SetFormula(C("A1"), "=SEQUENCE(3)");
        engine.SetFormula(C("C1"), "=SUM(A1#)");

        Assert.Equal(CellError.Spill, engine.GetValue(C("C1")).Error);

        engine.SetValue(C("A2"), CellValue.Empty);
        Assert.Equal(6, engine.GetValue(C("C1")).AsNumber());
    }
}

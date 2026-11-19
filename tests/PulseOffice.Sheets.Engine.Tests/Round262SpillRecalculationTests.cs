using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round262SpillRecalculationTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private readonly ISheetStructure _structure = new SheetStructure();

    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    [Fact]
    public void Unmerging_lets_a_blocked_spill_resume()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Sheets[0].Merges.Add(R("A3:B3"));
        engine.SetFormula(C("A1"), "=SEQUENCE(3)");
        Assert.Equal(CellError.Spill, engine.GetValue(C("A1")).Error);

        workbook.Sheets[0].Merges.RemoveOverlapping(R("A3"));

        Assert.Equal(3, engine.GetValue(C("A3")).AsNumber());
        Assert.True(engine.IsSpilledInto(C("A2")));
    }

    [Fact]
    public void Merging_over_a_spill_blocks_it()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("A1"), "=SEQUENCE(3)");
        Assert.True(engine.IsSpilledInto(C("A3")));

        workbook.Sheets[0].Merges.Add(R("A3:B3"));

        Assert.Equal(CellError.Spill, engine.GetValue(C("A1")).Error);
        Assert.False(engine.IsSpilledInto(C("A2")));
    }

    [Fact]
    public void Inserting_rows_above_moves_the_spill_with_its_anchor()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("A1"), "=SEQUENCE(3)");

        _structure.InsertRows(engine, 0, 0, 2);

        Assert.Equal(1, engine.GetValue(C("A3")).AsNumber());
        Assert.Equal(3, engine.GetValue(C("A5")).AsNumber());
        Assert.False(engine.IsSpilledInto(C("A2")));
        Assert.Equal(R("A3:A5"), engine.SpillRanges[C("A3")]);
    }

    [Fact]
    public void Inserting_a_row_inside_a_spill_spills_again_from_the_anchor()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("A1"), "=SEQUENCE(3)");

        _structure.InsertRows(engine, 0, 1, 1);

        Assert.Equal(2, engine.GetValue(C("A2")).AsNumber());
        Assert.Equal(3, engine.GetValue(C("A3")).AsNumber());
        Assert.True(engine.GetValue(C("A4")).IsEmpty);
    }

    [Fact]
    public void Deleting_the_row_that_blocked_a_spill_lets_it_resume()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(C("A2"), CellValue.Number(9));
        engine.SetFormula(C("A1"), "=SEQUENCE(2)");
        Assert.Equal(CellError.Spill, engine.GetValue(C("A1")).Error);

        _structure.DeleteRows(engine, 0, 1, 1);

        Assert.Equal(2, engine.GetValue(C("A2")).AsNumber());
    }

    [Fact]
    public void A_reader_of_the_spill_follows_a_column_insertion()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("A1"), "=SEQUENCE(1;3)");
        engine.SetFormula(C("A2"), "=SUM(A1#)");

        _structure.InsertColumns(engine, 0, 0, 1);

        Assert.Equal(6, engine.GetValue(C("B2")).AsNumber());
        Assert.Equal(3, engine.GetValue(C("D1")).AsNumber());
    }

    [Fact]
    public void Moving_a_spill_anchor_moves_the_spill()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var name = workbook.Sheets[0].Name;
        engine.SetFormula(C("A1"), "=SEQUENCE(2)");

        new SheetRangeMove(_structure).Move(engine, new RangeMove(name, R("A1"), name, new CellAddress(0, 2)));

        Assert.Equal(2, engine.GetValue(C("C2")).AsNumber());
        Assert.True(engine.GetValue(C("A2")).IsEmpty);
        Assert.False(engine.IsSpilledInto(C("A2")));
    }

    [Fact]
    public void A_volatile_spill_is_recomputed_after_a_structure_edit()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("A1"), "=RANDARRAY(2)");
        var before = engine.GetValue(C("A2")).AsNumber();

        _structure.InsertColumns(engine, 0, 0, 1);

        Assert.True(engine.IsSpilledInto(C("B2")));
        Assert.NotEqual(before, engine.GetValue(C("B2")).AsNumber());
    }

    [Fact]
    public void An_iterated_cycle_through_a_spilling_formula_still_spills()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Calculation = workbook.Calculation with { Iterative = true, MaxIterations = 10 };
        engine.SetFormula(C("B1"), "=A1");
        engine.SetFormula(C("A1"), "=SEQUENCE(2;1;B1*0+1)");

        Assert.Equal(1, engine.GetValue(C("A1")).AsNumber());
        Assert.Equal(2, engine.GetValue(C("A2")).AsNumber());
        Assert.True(engine.IsSpilledInto(C("A2")));
    }
}

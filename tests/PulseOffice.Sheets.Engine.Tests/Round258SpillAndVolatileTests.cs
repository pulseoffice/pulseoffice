using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round258SpillAndVolatileTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
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
    public void A_spill_over_merged_cells_is_blocked()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Sheets[0].Merges.Add(R("A3:B3"));

        engine.SetFormula(C("A1"), "=SEQUENCE(3)");

        Assert.Equal(CellError.Spill, engine.GetValue(C("A1")).Error);
        Assert.False(engine.IsSpilledInto(C("A2")));
    }

    [Fact]
    public void A_spill_into_a_table_is_blocked_and_resumes_when_the_table_goes()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Data", SheetIndex = 0, Range = R("A2:B4") });

        engine.SetFormula(C("A1"), "=SEQUENCE(2)");
        Assert.Equal(CellError.Spill, engine.GetValue(C("A1")).Error);

        workbook.DatabaseRanges.Remove("Data");
        Assert.Equal(2, engine.GetValue(C("A2")).AsNumber());
    }

    [Fact]
    public void RANDARRAY_is_volatile()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("A1"), "=RANDARRAY(1)");
        var first = engine.GetValue(C("A1")).AsNumber();

        var changed = false;
        for (var attempt = 0; attempt < 5 && !changed; attempt++)
        {
            engine.SetValue(C("C1"), CellValue.Number(attempt));
            changed = engine.GetValue(C("A1")).AsNumber() != first;
        }

        Assert.True(changed);
    }
}

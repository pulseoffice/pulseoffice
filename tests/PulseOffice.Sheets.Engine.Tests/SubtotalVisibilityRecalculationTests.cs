using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SubtotalVisibilityRecalculationTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    private (Workbook Workbook, CalculationEngine Engine) Filled()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        for (var row = 0; row < 5; row++) workbook.Sheets[0].SetValue(new CellAddress(row, 0), CellValue.Number(row + 1));
        engine.SetFormula(C("B1"), "=SUBTOTAL(109;A1:A5)");
        engine.SetFormula(C("B2"), "=AGGREGATE(9;5;A1:A5)");
        engine.SetFormula(C("B3"), "=B1*10");
        return (workbook, engine);
    }

    [Fact]
    public void HidingARowRecalculatesSubtotalAndItsDependents()
    {
        var (workbook, engine) = Filled();
        Assert.Equal(15, engine.GetValue(C("B1")).AsNumber());

        workbook.Sheets[0].Layout.HideRows(1, 2);

        Assert.Equal(10, engine.GetValue(C("B1")).AsNumber());
        Assert.Equal(10, engine.GetValue(C("B2")).AsNumber());
        Assert.Equal(100, engine.GetValue(C("B3")).AsNumber());

        workbook.Sheets[0].Layout.ShowRows(0, 4);

        Assert.Equal(15, engine.GetValue(C("B1")).AsNumber());
    }

    [Fact]
    public void CollapsingAGroupRecalculatesSubtotalOnce()
    {
        var (workbook, engine) = Filled();
        var sheet = workbook.Sheets[0];
        var outline = new SheetOutline();
        outline.Group(sheet, StructureAxis.Rows, 3, 4);
        var notifications = 0;
        workbook.RowVisibilityChanged += _ => notifications++;

        outline.Collapse(sheet, StructureAxis.Rows, 0);

        Assert.Equal(6, engine.GetValue(C("B1")).AsNumber());
        Assert.Equal(1, notifications);

        outline.Expand(sheet, StructureAxis.Rows, 0);

        Assert.Equal(15, engine.GetValue(C("B1")).AsNumber());
    }

    [Fact]
    public void ChangingAVisibleRowHeightDoesNotNotify()
    {
        var (workbook, _) = Filled();
        var notifications = 0;
        workbook.RowVisibilityChanged += _ => notifications++;

        workbook.Sheets[0].Layout.SetRowHeight(2, PulseOffice.Foundation.Units.Twip.FromPoints(40));

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void UnrelatedEditsDoNotRecalculateEverySubtotal()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 2000; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
            sheet[new CellAddress(row, 1)] = Cell.Blank with { Formula = "=SUBTOTAL(109;A$1:A$2000)" };
        }
        var engine = fixture.CreateEngine(workbook);
        engine.RecalculateAll();
        var clock = System.Diagnostics.Stopwatch.StartNew();

        for (var edit = 0; edit < 100; edit++) engine.SetValue(new SheetCell(0, new CellAddress(edit, 5)), CellValue.Number(edit));

        clock.Stop();
        Assert.True(clock.ElapsedMilliseconds < 1500, $"took {clock.ElapsedMilliseconds} ms");
        sheet.Layout.HideRow(1999);
        Assert.Equal(1998d * 1999 / 2, engine.GetValue(new SheetCell(0, new CellAddress(0, 1))).AsNumber());
    }
}

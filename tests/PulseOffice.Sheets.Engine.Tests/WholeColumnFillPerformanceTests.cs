using System.Diagnostics;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

[Collection(nameof(WholeColumnMillionRowPerformanceTests))]
public sealed class WholeColumnFillPerformanceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private const int Rows = 20_000;

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("=SUMIFS(B:B,A:A,3)", Rows / 10)]
    [InlineData("=COUNTIFS(A:A,\">=5\",B:B,1)", Rows / 2)]
    [InlineData("=SUM(B:B)", Rows)]
    [InlineData("=COUNT(A:A)", Rows)]
    [InlineData("=COUNTA(A:B)", 2 * Rows)]
    public void FillingRowsOneCellAtATimeUnderAWholeColumnFormulaStaysLinear(string formula, double expected)
    {
        var (_, engine) = fixture.CreateWorkbook();
        var cell = new SheetCell(0, new CellAddress(0, 4));
        engine.SetFormula(cell, formula);
        var clock = Stopwatch.StartNew();
        for (var row = 0; row < Rows; row++)
        {
            engine.SetValue(new SheetCell(0, new CellAddress(row, 0)), CellValue.Number(row % 10));
            engine.SetValue(new SheetCell(0, new CellAddress(row, 1)), CellValue.Number(1));
        }
        clock.Stop();

        Assert.Equal(expected, engine.GetValue(cell).AsNumber());
        Assert.True(clock.Elapsed < Bound, $"{formula} filling {Rows} rows took {clock.Elapsed}");
    }

    [Fact]
    public void EditingAnUnrelatedColumnDoesNotTouchAWholeColumnAggregate()
    {
        var (_, engine) = fixture.CreateWorkbook();
        for (var row = 0; row < Rows; row++)
            engine.SetValue(new SheetCell(0, new CellAddress(row, 0)), CellValue.Number(1));
        var cell = new SheetCell(0, new CellAddress(0, 4));
        engine.SetFormula(cell, "=SUM(A:A)");
        var clock = Stopwatch.StartNew();
        for (var row = 0; row < Rows; row++)
            engine.SetValue(new SheetCell(0, new CellAddress(row, 2)), CellValue.Number(row));
        clock.Stop();

        Assert.Equal(Rows, engine.GetValue(cell).AsNumber());
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"unrelated edits took {clock.Elapsed}");
    }
}

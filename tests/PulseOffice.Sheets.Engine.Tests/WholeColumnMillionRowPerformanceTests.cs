using System.Diagnostics;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

[CollectionDefinition(nameof(WholeColumnMillionRowPerformanceTests), DisableParallelization = true)]
public sealed class WholeColumnMillionRowPerformanceCollection;

[Collection(nameof(WholeColumnMillionRowPerformanceTests))]
public sealed class WholeColumnMillionRowPerformanceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private const int Rows = 1_000_000;

    private const int Keys = 10;

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(15);

    private CalculationEngine MillionRows()
    {
        var (_, engine) = fixture.CreateWorkbook();
        for (var row = 0; row < Rows; row++)
        {
            engine.SetValue(new SheetCell(0, new CellAddress(row, 0)), CellValue.Number(row % Keys));
            engine.SetValue(new SheetCell(0, new CellAddress(row, 1)), CellValue.Number(1));
        }

        return engine;
    }

    [Theory]
    [InlineData("=SUMIFS(B:B,A:A,3)", Rows / Keys)]
    [InlineData("=COUNTIFS(A:A,\">=5\",B:B,1)", Rows / 2)]
    [InlineData("=XLOOKUP(9,A:A,ROW(A:A))", 10)]
    [InlineData("=XLOOKUP(-1,A:A,B:B,0)", 0)]
    [InlineData("=ROWS(FILTER(A:A,A:A=3))", Rows / Keys)]
    [InlineData("=SUMPRODUCT((A:A=3)*B:B)", Rows / Keys)]
    public void WholeColumnFormulasOverAMillionRowsStayLinear(string formula, double expected)
    {
        var engine = MillionRows();
        var cell = new SheetCell(0, new CellAddress(0, 4));
        var clock = Stopwatch.StartNew();
        engine.SetFormula(cell, formula);
        var value = engine.GetValue(cell);
        clock.Stop();

        Assert.True(value.Kind == ValueKind.Number, $"{formula} gave {value}");
        Assert.Equal(expected, value.AsNumber());
        Assert.True(clock.Elapsed < Bound, $"{formula} over {Rows} rows took {clock.Elapsed}");
    }
}

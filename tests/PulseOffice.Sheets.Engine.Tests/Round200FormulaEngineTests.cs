using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round200FormulaEngineTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private CellValue Evaluate(string formula)
    {
        var (_, engine) = fixture.CreateWorkbook();
        Assert.True(CellAddress.TryParseA1("A1", out var address));
        var cell = new SheetCell(0, address);
        engine.SetFormula(cell, formula);
        return engine.GetValue(cell);
    }

    [Fact]
    public void Reduce_keeps_folding_after_an_intermediate_error_the_lambda_can_absorb() =>
        Assert.Equal(5, Evaluate("=REDUCE(0;{1;2;5};LAMBDA(a;b;IF(b=2;NA();IFERROR(a;0)+b)))").AsNumber());

    [Fact]
    public void Sequence_computes_each_term_from_the_start_without_accumulated_drift() =>
        Assert.Equal(0.3, Evaluate("=INDEX(SEQUENCE(4;1;0;0.1);4)").AsNumber());
}

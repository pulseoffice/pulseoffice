using System.Diagnostics;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DeepAuditLedgerFunctionsRound16Tests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell At(int row, int column) => new(0, new CellAddress(row, column));

    private CellValue? Compute(string formula, TimeSpan limit, Action<Recalculation.CalculationEngine>? prepare = null)
    {
        var (_, engine) = fixture.CreateWorkbook();
        prepare?.Invoke(engine);
        CellValue? result = null;
        var thread = new Thread(() =>
        {
            engine.SetFormula(At(1200, 3), formula);
            result = engine.GetValue(At(1200, 3));
        }, 16 * 1024 * 1024) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(limit), $"{formula} did not finish within {limit}");
        return result;
    }

    [Theory]
    [InlineData("=SUBSTITUTE(REPT(\"a\";1000);\"a\";REPT(\"b\";1000))")]
    [InlineData("=REGEX(REPT(\"a\";1000);\"a\";REPT(\"b\";100);\"g\")")]
    [InlineData("=TEXTJOIN(\",\";TRUE;REPT(\"a\";30000);REPT(\"b\";30000))")]
    [InlineData("=CONCAT(REPT(\"a\";30000);REPT(\"b\";30000))")]
    [InlineData("=CONCATENATE(REPT(\"a\";30000);REPT(\"b\";30000))")]
    [InlineData("=REPT(\"a\";30000)&REPT(\"b\";30000)")]
    public void ATextResultLongerThanACellCanHoldIsAValueError(string formula)
    {
        var result = Compute(formula, TimeSpan.FromSeconds(30));
        Assert.Equal(CellValue.FromError(CellError.Value), result);
    }

    [Fact]
    public void TextAtTheLimitIsStillProduced()
    {
        var result = Compute("=LEN(SUBSTITUTE(REPT(\"a\";100);\"a\";REPT(\"b\";327))&REPT(\"c\";67))", TimeSpan.FromSeconds(30));
        Assert.Equal(CellValue.Number(32_767), result);
    }

    [Fact]
    public void MatrixProductBeyondTheArrayCapIsRefused()
    {
        var result = Compute("=ROWS(MMULT(SEQUENCE(5000);SEQUENCE(1;5000)))", TimeSpan.FromSeconds(60));
        Assert.Equal(CellValue.FromError(CellError.Number), result);
    }

    [Fact]
    public void ATallTimesWideBroadcastBeyondTheArrayCapIsRefused()
    {
        var result = Compute("=ROWS(SEQUENCE(5000)*SEQUENCE(1;5000))", TimeSpan.FromSeconds(60));
        Assert.Equal(CellValue.FromError(CellError.Number), result);
    }

    [Fact]
    public void LiftingAScalarFunctionOverAHugeRangeIsRefused()
    {
        var result = Compute("=ROWS(ABS(A1:XFD1100))", TimeSpan.FromSeconds(60), engine => engine.SetValue(At(0, 0), CellValue.Number(2)));
        Assert.Equal(CellValue.FromError(CellError.Number), result);
    }

    [Fact]
    public void UniqueOverManyDistinctRowsIsFast()
    {
        var watch = Stopwatch.StartNew();
        var result = Compute("=ROWS(UNIQUE(SEQUENCE(60000)))", TimeSpan.FromSeconds(60));
        Assert.Equal(CellValue.Number(60000), result);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
    }

    [Fact]
    public void UniqueComparesTextWithoutCaseAndKeepsLogicalsApartFromNumbers()
    {
        var result = Compute("=ROWS(UNIQUE({\"a\"|\"A\"|\"b\"|1|TRUE|1}))", TimeSpan.FromSeconds(10));
        Assert.Equal(CellValue.Number(4), result);
    }

    [Fact]
    public void FrequencyOverManyBinsIsFastAndCorrect()
    {
        var watch = Stopwatch.StartNew();
        var result = Compute("=SUM(FREQUENCY(SEQUENCE(100000);SEQUENCE(5000;1;1;10)))+INDEX(FREQUENCY(SEQUENCE(100000);SEQUENCE(5000;1;1;10));5001)/1000000", TimeSpan.FromSeconds(60));
        Assert.Equal(100000 + 50009 / 1_000_000.0, result!.Value.AsNumber(), 9);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
    }

    [Theory]
    [InlineData("=SUM(FREQUENCY({1;2;2;3;5;8};{2;5}))", 6)]
    [InlineData("=INDEX(FREQUENCY({1;2;2;3;5;8};{5;2});1)", 2)]
    [InlineData("=INDEX(FREQUENCY({1;2;2;3;5;8};{5;2});2)", 3)]
    [InlineData("=INDEX(FREQUENCY({1;2;2;3;5;8};{5;2});3)", 1)]
    [InlineData("=INDEX(FREQUENCY({1;2;2;3;5;8};{2;2});2)", 0)]
    public void FrequencyKeepsTheBinOrder(string formula, double expected)
    {
        var result = Compute(formula, TimeSpan.FromSeconds(10));
        Assert.Equal(CellValue.Number(expected), result);
    }

    [Fact]
    public void WildcardCriteriaDoNotBacktrackCatastrophically()
    {
        var watch = Stopwatch.StartNew();
        var result = Compute("=COUNTIF(A1:A20;\"*a*a*a*a*a*a*a*a*a*b\")+COUNTIF(A1:A20;\"*a*a\")", TimeSpan.FromSeconds(120), engine =>
        {
            for (var row = 0; row < 20; row++) engine.SetValue(At(row, 0), CellValue.Text(new string('a', 80)));
        });
        Assert.Equal(CellValue.Number(20), result);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
    }

    [Theory]
    [InlineData("=IRR({-100;230;-132})", 0.1)]
    [InlineData("=IRR({-100;230;-132};0.25)", 0.2)]
    [InlineData("=IRR({-70000;12000;15000;18000;21000;26000})", 0.0866309480365316)]
    [InlineData("=RATE(48;-200;8000)", 0.00770147248820189)]
    [InlineData("=XIRR({-10000;2750;4250;3250;2750};{39448;39508;39751;39859;39904})", 0.3733625335)]
    public void RateSolversFindTheRootNearestTheGuess(string formula, double expected)
    {
        var result = Compute(formula, TimeSpan.FromSeconds(10));
        Assert.True(result is { Kind: ValueKind.Number }, result?.ToString());
        Assert.Equal(expected, result!.Value.AsNumber(), 6);
    }

    [Theory]
    [InlineData("=XIRR({-100;110};{45000;44000})")]
    [InlineData("=XIRR({100;110};{45000;45100})")]
    public void XirrRejectsDatesBeforeTheStartAndCashFlowsOfOneSign(string formula)
    {
        var result = Compute(formula, TimeSpan.FromSeconds(10));
        Assert.Equal(CellValue.FromError(CellError.Number), result);
    }

    [Theory]
    [InlineData("=SORT({3;1;2};1;0)")]
    [InlineData("=SORT({3;1;2};1;2)")]
    public void SortRejectsAnOrderOtherThanOneOrMinusOne(string formula)
    {
        var result = Compute(formula, TimeSpan.FromSeconds(10));
        Assert.Equal(CellValue.FromError(CellError.Value), result);
    }
}

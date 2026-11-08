using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DeepAuditLedgerFunctionsRound17Tests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell At(int row, int column) => new(0, new CellAddress(row, column));

    private CellValue? Compute(string formula, TimeSpan limit)
    {
        var (_, engine) = fixture.CreateWorkbook();
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

    private CellValue? Compute(string formula) => Compute(formula, TimeSpan.FromSeconds(10));

    private double Number(string formula)
    {
        var result = Compute(formula);
        Assert.True(result is { Kind: ValueKind.Number }, $"{formula} gave {result}");
        return result!.Value.AsNumber();
    }

    [Theory]
    [InlineData("=COMBIN(1E+10;5E+9)")]
    [InlineData("=COMBIN(1E+15;5E+14)")]
    [InlineData("=COMBINA(1E+10;5E+9)")]
    [InlineData("=PERMUT(1E+10;1E+9)")]
    [InlineData("=FACTDOUBLE(1E+10)")]
    [InlineData("=MULTINOMIAL(1E+9;1E+9)")]
    public void CombinatoricsThatOverflowEndQuicklyInANumberError(string formula) =>
        Assert.Equal(CellValue.FromError(CellError.Number), Compute(formula, TimeSpan.FromSeconds(5)));

    [Fact]
    public void HypergeometricCumulativeOverAHugePopulationIsQuick()
    {
        var result = Compute("=HYPGEOM.DIST(5E+8;1E+9;1E+9;2E+9;TRUE)", TimeSpan.FromSeconds(5));
        Assert.True(result is { Kind: ValueKind.Number }, $"gave {result}");
        Assert.InRange(result!.Value.AsNumber(), 0.5, 0.51);
    }

    [Theory]
    [InlineData("=HYPGEOM.DIST(1;4;8;20;FALSE)", 0.363261094, 1e-9)]
    [InlineData("=HYPGEOM.DIST(1;4;8;20;TRUE)", 0.465428277, 1e-9)]
    [InlineData("=HYPGEOMDIST(1;4;8;20)", 0.363261094, 1e-9)]
    [InlineData("=HYPGEOM.DIST(500;1000;1000;2000;FALSE)", 0.0357, 5e-4)]
    [InlineData("=HYPGEOM.DIST(400;1000;1000;2000;TRUE)", 0, 1e-15)]
    [InlineData("=HYPGEOM.DIST(600;1000;1000;2000;TRUE)", 1, 1e-15)]
    [InlineData("=NEGBINOM.DIST(10;5;0.25;FALSE)", 0.0550486604, 1e-9)]
    [InlineData("=NEGBINOM.DIST(2000;2000;0.5;FALSE)", 0.0063, 2e-4)]
    [InlineData("=NEGBINOMDIST(2000;2000;0.5)", 0.0063, 2e-4)]
    [InlineData("=FISHERINV(400)", 1, 0)]
    [InlineData("=FISHERINV(-400)", -1, 0)]
    [InlineData("=BITLSHIFT(4;2)", 16, 0)]
    [InlineData("=BITRSHIFT(13;2)", 3, 0)]
    [InlineData("=ROUND(5;-1E+10)", 0, 0)]
    [InlineData("=TRUNC(5;-1E+10)", 0, 0)]
    [InlineData("=ROUNDDOWN(5;-1E+10)", 0, 0)]
    [InlineData("=ROUNDUP(5.5;1E+10)", 5.5, 0)]
    [InlineData("=PERCENTILE.EXC({1;2;3};0.25)", 1, 0)]
    [InlineData("=PERCENTILE.EXC({1;2;3};0.75)", 3, 0)]
    [InlineData("=QUARTILE.EXC({1;2;3};1)", 1, 0)]
    [InlineData("=QUARTILE.EXC({1;2;3};3)", 3, 0)]
    [InlineData("=XMATCH(25;{10|20|30};-1;2)", 2, 0)]
    [InlineData("=XMATCH(25;{10|20|30};1;2)", 3, 0)]
    [InlineData("=XMATCH(25;{30|20|10};-1;-2)", 2, 0)]
    [InlineData("=XMATCH(25;{30|20|10};1;-2)", 1, 0)]
    [InlineData("=XMATCH(20;{10|20|20|30};0;2)", 2, 0)]
    [InlineData("=XLOOKUP(\"b\";{\"a\"|\"b\"|\"c\"};{1|2|3};;0;2)", 2, 0)]
    public void EdgeCasesMatchTheSpreadsheetReference(string formula, double expected, double tolerance) =>
        Assert.Equal(expected, Number(formula), tolerance);

    [Theory]
    [InlineData("=BITLSHIFT(140737488355328;17)", CellError.Number)]
    [InlineData("=BITAND(1.5;1)", CellError.Number)]
    [InlineData("=BITOR(1;2.5)", CellError.Number)]
    [InlineData("=PERCENTILE.EXC({1;2;3};0.2)", CellError.Number)]
    [InlineData("=XMATCH(2;{1;2|3;4})", CellError.Value)]
    [InlineData("=XLOOKUP(2;{1;2|3;4};{1;2|3;4})", CellError.Value)]
    [InlineData("=XLOOKUP(3;{1|2|3};{10|20})", CellError.Value)]
    [InlineData("=XMATCH(\"a*\";{\"ab\"|\"b\"};2;2)", CellError.Value)]
    [InlineData("=XMATCH(2;{5|2|1};0;2)", CellError.NotAvailable)]
    [InlineData("=COT(0)", CellError.DivideByZero)]
    [InlineData("=CSC(0)", CellError.DivideByZero)]
    [InlineData("=COTH(0)", CellError.DivideByZero)]
    [InlineData("=CSCH(0)", CellError.DivideByZero)]
    [InlineData("=BASE(9007199254740992;2)", CellError.Number)]
    [InlineData("=DECIMAL(\"FFFFFFFFFFFFFFFF\";16)", CellError.Number)]
    [InlineData("=BETA.INV(0.5;2;3;1;1)", CellError.Number)]
    [InlineData("=BETAINV(0.5;2;3;2;1)", CellError.Number)]
    [InlineData("=F.TEST({1;1;1};{1;2;3})", CellError.DivideByZero)]
    public void EdgeCasesGiveTheSpreadsheetReferenceError(string formula, CellError expected) =>
        Assert.Equal(CellValue.FromError(expected), Compute(formula));

    [Fact]
    public void LargeOverAnArrayOfRanksSortsTheSampleOnce()
    {
        var result = Compute("=SUM(LARGE(SEQUENCE(30000);SEQUENCE(30000)))", TimeSpan.FromSeconds(10));
        Assert.Equal(CellValue.Number(450_015_000), result);
    }

    [Fact]
    public void CountIfOverMoreCellsThanAnIntegerHoldsCountsThemAll()
    {
        var result = Compute("=COUNTIF(E:XFD;\"\")", TimeSpan.FromSeconds(30));
        Assert.Equal(CellValue.Number(16_380d * 1_048_576), result);
    }
}

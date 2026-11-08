using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class CalcErrorTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private CellValue Evaluate(string formula)
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        for (var row = 0; row < 3; row++)
        {
            workbook.Sheets[0].SetValue(new CellAddress(row, 0), CellValue.Number(row + 1));
            workbook.Sheets[0].SetValue(new CellAddress(row, 1), CellValue.Number(row + 1));
        }
        engine.RecalculateAll();
        var target = new SheetCell(0, new CellAddress(20, 5));
        engine.SetFormula(target, formula);
        return engine.GetValue(target);
    }

    [Theory]
    [InlineData("=FILTER(A1:A3;A1:A3>10)")]
    [InlineData("=UNIQUE(B1:B3;FALSE;TRUE)+UNIQUE({1|1};FALSE;TRUE)")]
    [InlineData("=UNIQUE({1|1};FALSE;TRUE)")]
    [InlineData("=TAKE(A1:A3;0)")]
    [InlineData("=DROP(A1:A3;3)")]
    [InlineData("=DROP(A1:A3;-5)")]
    [InlineData("=SEQUENCE(0)")]
    [InlineData("=SEQUENCE(2;0)")]
    [InlineData("=RANDARRAY(0)")]
    [InlineData("=TOCOL(C1:C3;1)")]
    [InlineData("=TOROW(C1:C3;3)")]
    [InlineData("=LAMBDA(x;x+1)")]
    [InlineData("=LET(f;LAMBDA(x;x);f)")]
    [InlineData("=INDEX(BYROW(A1:B3;LAMBDA(r;r));1;1)")]
    [InlineData("=INDEX(MAP(A1:A3;LAMBDA(v;SEQUENCE(2)));1;1)")]
    [InlineData("=INDEX(MAKEARRAY(2;2;LAMBDA(r;c;A1:A2));1;1)")]
    [InlineData("=#CALC!")]
    public void Excel_gives_calc_error(string formula) =>
        Assert.Equal(CellError.Calc, Evaluate(formula).Error);

    [Theory]
    [InlineData("=SEQUENCE(-1)", CellError.Value)]
    [InlineData("=RANDARRAY(-1)", CellError.Value)]
    [InlineData("=FILTER(A1:A3;A1:A3>10;\"none\")", CellError.None)]
    public void Negative_sizes_and_if_empty_are_not_calc(string formula, CellError expected) =>
        Assert.Equal(expected, Evaluate(formula).Error);

    [Theory]
    [InlineData("=ERROR.TYPE(SEQUENCE(0))", 14)]
    [InlineData("=ERROR.TYPE(#SPILL!)", 9)]
    [InlineData("=IFERROR(FILTER(A1:A3;A1:A3>10);7)", 7)]
    [InlineData("=IF(ISERROR(TAKE(A1:A3;0));1;0)", 1)]
    [InlineData("=IF(ISERR(TAKE(A1:A3;0));1;0)", 1)]
    [InlineData("=IF(ISNA(TAKE(A1:A3;0));1;0)", 0)]
    [InlineData("=IFNA(TAKE(A1:A3;0);1)+0", -1)]
    public void Calc_error_is_seen_by_the_information_functions(string formula, double expected)
    {
        var value = Evaluate(formula);
        if (expected < 0) Assert.Equal(CellError.Calc, value.Error);
        else Assert.Equal(expected, value.AsNumber());
    }

    [Fact]
    public void Calc_error_text_is_the_excel_one() =>
        Assert.Equal("#CALC!", CellValue.FromError(CellError.Calc).ErrorText);

    [Fact]
    public void Map_with_scalar_results_still_works() =>
        Assert.Equal(4, Evaluate("=INDEX(MAP(A1:A3;LAMBDA(v;v*2));2;1)").AsNumber());
}

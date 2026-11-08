using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class FormatInferenceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private readonly NumberFormatter _formatter = new(CultureInfo.InvariantCulture,
        serial => new DateOnly(1899, 12, 30).ToDateTime(TimeOnly.MinValue).AddDays(serial));

    private (Workbook Workbook, Recalculation.CalculationEngine Engine) Sheet()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        Put(workbook, new CellAddress(0, 0), CellValue.Number(42212), "m/d/yy");
        Put(workbook, new CellAddress(1, 0), CellValue.Number(3), null);
        Put(workbook, new CellAddress(2, 0), CellValue.Number(0.25), "0.0%");
        Put(workbook, new CellAddress(3, 0), CellValue.Number(12.5), "#,##0.00 \"€\"");
        Put(workbook, new CellAddress(4, 0), CellValue.Number(0.5), "h:mm:ss");
        Put(workbook, new CellAddress(5, 0), CellValue.Number(42205), "m/d/yy");
        return (workbook, engine);
    }

    private static void Put(Workbook workbook, CellAddress address, CellValue value, string? code)
    {
        var sheet = workbook.Sheets[0];
        sheet.SetValue(address, value);
        if (code is null) return;
        var id = workbook.NumberFormats.Register(code);
        sheet.SetStyle(address, workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = id }));
    }

    private string Shown(string formula)
    {
        var (workbook, engine) = Sheet();
        var target = new SheetCell(0, new CellAddress(0, 2));
        engine.SetFormula(target, formula);
        var cell = workbook.Sheets[0][target.Address];
        return new CellPresenter(_formatter).Present(cell, workbook.Styles, workbook.NumberFormats, 30).Text;
    }

    [Theory]
    [InlineData("=A1", "7/27/15")]
    [InlineData("=A1-A2", "24/07/2015")]
    [InlineData("=A1+A2", "30/07/2015")]
    [InlineData("=A1-A6", "7")]
    [InlineData("=A5+A5", "24:00:00")]
    [InlineData("=A1+A5", "27/07/2015 12:00")]
    [InlineData("=A3+A2", "325.00%")]
    [InlineData("=A3*A2", "0.75")]
    [InlineData("=A4*2", "25.00 €")]
    [InlineData("=2/A4", "0.16")]
    [InlineData("=SUM(A1:A1)", "7/27/15")]
    [InlineData("=SUM(A3:A4)", "12.75 €")]
    [InlineData("=SUM(A2;A1)", "42215")]
    [InlineData("=AVERAGE(A3;A3)", "25.00%")]
    [InlineData("=DATE(2020;1;1)", "01/01/2020")]
    [InlineData("=TIME(1;2;3)", "01:02:03")]
    [InlineData("=IF(0;A2;A1)", "7/27/15")]
    [InlineData("=CHOOSE(1;A1;A2)", "7/27/15")]
    [InlineData("=INDEX(A1:A6;1)", "7/27/15")]
    [InlineData("=-A1", "-42212")]
    [InlineData("=ROUND(A4;0)", "13")]
    [InlineData("=5%", "5.00%")]
    public void IlFormatoDelRisultatoSiDeduceComeInCalc(string formula, string expected) =>
        Assert.Equal(expected, Shown(formula));

    [Fact]
    public void UnFormatoSceltoSullaCellaVinceSuQuelloDedotto()
    {
        var (workbook, engine) = Sheet();
        var target = new SheetCell(0, new CellAddress(0, 2));
        Put(workbook, target.Address, CellValue.Empty, "0.00");
        engine.SetFormula(target, "=A1-A2");
        var cell = workbook.Sheets[0][target.Address] with { StyleId = workbook.Sheets[0].StyleIdAt(target.Address) };

        Assert.Equal("42209.00", new CellPresenter(_formatter).Present(cell, workbook.Styles, workbook.NumberFormats, 30).Text);
    }

    [Fact]
    public void UnaCostanteNonUsaIlFormatoDedottoDiPrima()
    {
        var (workbook, engine) = Sheet();
        var target = new SheetCell(0, new CellAddress(0, 2));
        engine.SetFormula(target, "=A1");
        engine.SetValue(target, CellValue.Number(5));

        var cell = workbook.Sheets[0][target.Address];
        Assert.Equal("5", new CellPresenter(_formatter).Present(cell, workbook.Styles, workbook.NumberFormats, 30).Text);
    }
}

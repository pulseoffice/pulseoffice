using System.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class LedgerAuditSecondRoundTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1, int sheet = 0)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(sheet, address);
    }

    private CellValue Valuta(string formula, CalculationSettings? settings = null, bool legacy = false, string where = "H1")
    {
        var workbook = Workbook.CreateDefault();
        if (settings is not null) workbook.Calculation = settings;
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 5; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row + 1));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Number((row + 1) * 10));
        }
        var target = C(where);
        sheet[target.Address] = new Cell { Formula = formula, ImplicitIntersection = legacy };
        var engine = fixture.CreateEngine(workbook);
        engine.RecalculateAll();
        return engine.GetValue(target);
    }

    [Theory]
    [InlineData("=SUM(A1:A2~B1:B2)", 33)]
    [InlineData("=COUNT(A1~B1~A3)", 3)]
    [InlineData("=MAX(A1:A2~B5)", 50)]
    [InlineData("=AVERAGE(A1~A3)", 2)]
    public void La_concatenazione_di_riferimenti_tilde_come_nel_riferimento(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber());

    [Fact]
    public void La_tilde_si_ristampa_uguale() =>
        Assert.Equal("SUM(A1:A2~B1:B2)", Canonico("=SUM(A1:A2~B1:B2)"));

    [Fact]
    public void I_riferimenti_3D_sommano_tutti_i_fogli_e_seguono_le_modifiche()
    {
        const string formula = "=SUM(Primo.A1:Terzo.A1)";
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Name = "Primo";
        workbook.AddSheet("Secondo");
        workbook.AddSheet("Terzo");
        for (var index = 0; index < 3; index++) workbook.Sheets[index].SetValue(new CellAddress(0, 0), CellValue.Number(index == 0 ? 1 : 100));
        var engine = fixture.CreateEngine(workbook);
        engine.SetFormula(C("B1"), formula);
        Assert.Equal(201, engine.GetValue(C("B1")).AsNumber());

        engine.SetValue(C("A1", 1), CellValue.Number(5));
        Assert.Equal(106, engine.GetValue(C("B1")).AsNumber());
    }

    [Fact]
    public void Il_riferimento_3D_si_ristampa_con_entrambi_i_fogli() =>
        Assert.Equal("SUM(Primo.A1:Terzo.B2)", Canonico("=SUM(Primo.A1:Terzo.B2)"));

    [Theory]
    [InlineData("=3+CURRENT()", 6)]
    [InlineData("=SUM(5;CURRENT())", 10)]
    [InlineData("=CURRENT()", 0)]
    public void CURRENT_restituisce_il_valore_appena_calcolato(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber());

    [Fact]
    public void CONCATENATE_su_intervallo_usa_l_intersezione_implicita() =>
        Assert.Equal("2", Valuta("=CONCATENATE(A1:A5)", legacy: true, where: "D2").AsText());

    [Theory]
    [InlineData("=LEFTB(\"日本語\";1)", " ")]
    [InlineData("=RIGHTB(\"日本語\";1)", " ")]
    [InlineData("=MIDB(\"日本語\";2;2)", "  ")]
    [InlineData("=MIDB(\"日本語\";2;3)", " 本")]
    [InlineData("=REPLACEB(\"日本語\";2;2;\"x\")", " x 語")]
    [InlineData("=REPLACEB(\"日本語\";1;1;\"x\")", "x 本語")]
    [InlineData("=REPLACEB(\"日本語\";2;1;\"x\")", " x本語")]
    [InlineData("=REPLACEB(\"日本語\";3;2;\"x\")", "日x語")]
    [InlineData("=LEFTB(\"a日b\";2)", "a ")]
    [InlineData("=RIGHTB(\"a日b\";2)", " b")]
    [InlineData("=REPLACEB(\"a日b\";3;1;\"x\")", "a xb")]
    public void Le_funzioni_a_byte_spezzano_i_caratteri_doppi_come_nel_riferimento(string formula, string atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsText());

    [Fact]
    public void SUMPRODUCT_conta_i_booleani_come_ODF_e_li_ignora_come_Excel()
    {
        Assert.Equal(2, Valuta("=SUMPRODUCT(A1:A3>1)").AsNumber());
        Assert.Equal(0, Valuta("=SUMPRODUCT(A1:A3>1)", CalculationSettings.Default with { BooleansAreNumbersInArrays = false }).AsNumber());
    }

    [Theory]
    [InlineData("=FORECAST.ETS.ADD(25;B1:B24;A1:A24)", 102.316666666667)]
    [InlineData("=FORECAST.ETS.ADD(27.5;B1:B24;A1:A24;0)", 113.36270515994)]
    [InlineData("=FORECAST.ETS.ADD(30;B1:B24;A1:A24;6)", 121.315986626941)]
    [InlineData("=FORECAST.ETS.MULT(26;B1:B24;A1:A24;6)", 106.044379924396)]
    [InlineData("=FORECAST.ETS.MULT(25;B1:B24;A1:A24;0)", 107.721908559706)]
    [InlineData("=FORECAST.ETS.SEASONALITY(B1:B24;A1:A24)", 12)]
    [InlineData("=FORECAST.ETS.STAT.ADD(B1:B24;A1:A24;1;6)", 0.03125)]
    [InlineData("=FORECAST.ETS.STAT.ADD(B1:B24;A1:A24;2;6)", 0.5)]
    [InlineData("=FORECAST.ETS.STAT.ADD(B1:B24;A1:A24;3;6)", 0.337890625)]
    [InlineData("=FORECAST.ETS.STAT.ADD(B1:B24;A1:A24;4;6)", 0.406914690481045)]
    [InlineData("=FORECAST.ETS.STAT.ADD(B1:B24;A1:A24;5;6)", 0.0269521509226301)]
    [InlineData("=FORECAST.ETS.STAT.ADD(B1:B24;A1:A24;7;0)", 4.51002659786146)]
    [InlineData("=FORECAST.ETS.PI.ADD(28;B1:B24;A1:A24;0.9;0)", 21.4681118671002)]
    [InlineData("=FORECAST.ETS.ADD(D25;B1:B24;D1:D24)", 102.316666666667)]
    public void FORECAST_ETS_coincide_con_il_riferimento(string formula, double reference)
    {
        var (_, engine) = fixture.CreateWorkbook();
        var epoch = new DateOnly(1899, 12, 30);
        for (var r = 1; r <= 24; r++)
        {
            engine.SetValue(C($"A{r}"), CellValue.Number(r));
            engine.SetValue(C($"B{r}"), CellValue.Number(50 + 2 * r + ((r * 7) % 5) * 1.7 + (r % 6 == 0 ? 8 : 0) + ((r * r) % 3) * 0.9));
        }
        for (var m = 0; m < 25; m++)
            engine.SetValue(C($"D{m + 1}"), CellValue.Number(new DateOnly(2020 + m / 12, m % 12 + 1, 15).DayNumber - epoch.DayNumber));
        engine.SetFormula(C("F1"), formula);
        var value = engine.GetValue(C("F1")).AsNumber();
        Assert.True(Math.Abs(value - reference) <= 1e-12 * Math.Abs(reference), $"{formula}: {value} invece di {reference}");
    }

    [Theory]
    [InlineData("=BASE(5;2;-4)", "#NUM!")]
    [InlineData("=WORKDAY.INTL(DATE(2014;11;1);2;\"1111111\")", "#VALUE!")]
    [InlineData("=WORKDAY.INTL(DATE(2014;11;1);2;8)", "#NUM!")]
    [InlineData("=WORKDAY.INTL(DATE(2014;11;1);2;0)", "#VALUE!")]
    [InlineData("=NETWORKDAYS.INTL(DATE(2006;1;1);DATE(2006;2;1);\"1111111\")", "0")]
    [InlineData("=CELL(\"ROW\")", "1")]
    [InlineData("=AREAS(A1:B3~F2~G1)", "3")]
    [InlineData("=ROWS(A1:A2~B1)", "#VALUE!")]
    [InlineData("=RANK(2;A1:A2~B1:B2)", "3")]
    [InlineData("=IRR(A1:A2~B1)", "#VALUE!")]
    public void Casi_limite_come_nel_riferimento_senza_eccezioni(string formula, string atteso) =>
        Assert.Equal(atteso, Valuta(formula).ToString());

    private static string Canonico(string formula)
    {
        var builder = new StringBuilder();
        new FormulaParser().Parse(formula).Accept(new CanonicalFormulaPrinter(builder, new BinaryOperators()));
        return builder.ToString();
    }
}

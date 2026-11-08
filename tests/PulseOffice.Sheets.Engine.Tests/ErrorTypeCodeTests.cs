using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class ErrorTypeCodeTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private CellValue Valuta(string formula)
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        for (var row = 1; row <= 6; row++)
            for (var column = 0; column < 3; column++)
            {
                Assert.True(CellAddress.TryParseA1($"{(char)('J' + column)}{row}", out var address));
                workbook.Sheets[0].SetValue(address, CellValue.Number(row * 10 + column));
            }
        engine.RecalculateAll();
        Assert.True(CellAddress.TryParseA1("Z99", out var target));
        engine.SetFormula(new SheetCell(0, target), formula);
        return engine.GetValue(new SheetCell(0, target));
    }

    [Theory]
    [InlineData("=DROP(J1:L6;30)", CellError.Calc)]
    [InlineData("=DROP(J1:L6;-30)", CellError.Calc)]
    [InlineData("=DROP(J1:L6)", CellError.Number)]
    [InlineData("=TAKE(J1:L6)", CellError.Number)]
    [InlineData("=TAKE(J1:L6;0)", CellError.Calc)]
    [InlineData("=EXPAND(J1:L6;2)", CellError.Number)]
    [InlineData("=CHOOSEROWS(J1:L6;{5|30})", CellError.Value)]
    public void Gli_errori_delle_funzioni_di_matrice_sono_quelli_di_riferimento(string formula, CellError atteso) =>
        Assert.Equal(atteso, Valuta(formula).Error);

    [Fact]
    public void Take_oltre_la_misura_restituisce_tutto()
    {
        var value = Valuta("=INDEX(TAKE(J1:L6;30);6;3)");
        Assert.Equal(62, value.AsNumber());
    }

    [Theory]
    [InlineData("=LOOKUP(20;J1:J6;K1:L6)", CellError.Value)]
    [InlineData("=LOOKUP(Q1;J1:J6;K1:K6)", CellError.NotAvailable)]
    [InlineData("=MMULT(J1:L6;J1:L6)", CellError.Number)]
    [InlineData("=CHOOSE(0;1;2)", CellError.Number)]
    [InlineData("=ADDRESS(0;1)", CellError.Number)]
    [InlineData("=ADDRESS(1;1;0)", CellError.Value)]
    public void Gli_errori_di_ricerca_e_matrici_sono_quelli_di_riferimento(string formula, CellError atteso) =>
        Assert.Equal(atteso, Valuta(formula).Error);

    [Fact]
    public void Lookup_legge_il_vettore_risultato_oltre_la_sua_fine_come_nel_riferimento()
    {
        Assert.Equal(61, Valuta("=LOOKUP(60;J1:J6;K1:K2)").AsNumber());
    }

    [Fact]
    public void Lookup_confronta_il_testo_senza_badare_alle_maiuscole()
    {
        Assert.Equal("Bruce", Valuta("=LOOKUP(\"b\";{\"a\";\"B\";\"c\"};{\"Andy\";\"Bruce\";\"Charlie\"})").AsText());
    }

    [Theory]
    [InlineData("=CONVERT(1;\"uk_acre\";\"us_acre\")", 0.999996000004)]
    [InlineData("=CONVERT(1;\"m2\";\"ft2\")", 10.7639104167097)]
    [InlineData("=CONVERT(1;\"cm3\";\"l\")", 0.001)]
    [InlineData("=CONVERT(1;\"kibyte\";\"bit\")", 8192)]
    [InlineData("=CONVERT(100;\"C\";\"F\")", 212)]
    [InlineData("=CONVERT(1;\"m^2\";\"cm^2\")", 10000)]
    public void Convert_usa_la_tabella_delle_unita_di_riferimento(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber(), 9);

    [Fact]
    public void Un_intervallo_di_database_si_usa_come_nome_nelle_formule()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Dati", SheetIndex = 0, Range = new CellRange(new CellAddress(0, 9), new CellAddress(5, 11)) });
        for (var row = 0; row < 6; row++) workbook.Sheets[0].SetValue(new CellAddress(row, 9), CellValue.Number(row + 1));
        engine.RecalculateAll();
        engine.SetFormula(new SheetCell(0, new CellAddress(98, 25)), "=SUM(Dati)");
        Assert.Equal(21, engine.GetValue(new SheetCell(0, new CellAddress(98, 25))).AsNumber());
    }

    [Fact]
    public void Il_criterio_di_testo_vuoto_dei_database_accetta_ogni_riga()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("a"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("b"));
        for (var row = 1; row <= 3; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Number(row * 10));
        }
        sheet.SetValue(new CellAddress(5, 0), CellValue.Text("a"));
        engine.RecalculateAll();
        engine.SetFormula(new SheetCell(0, new CellAddress(6, 0)), "=\"\"");
        engine.SetFormula(new SheetCell(0, new CellAddress(98, 25)), "=DSUM(A1:B4;\"b\";A6:A7)");
        Assert.Equal(60, engine.GetValue(new SheetCell(0, new CellAddress(98, 25))).AsNumber());
    }

    [Fact]
    public void Formula_mostra_la_formula_matrice_fra_graffe_in_ogni_cella_della_matrice()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Sheets[0].SetArrayFormula(new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)), "=ROW(B1:B3)");
        engine.RecalculateAll();
        engine.SetFormula(new SheetCell(0, new CellAddress(98, 25)), "=FORMULA(A2)");
        engine.SetFormula(new SheetCell(0, new CellAddress(97, 25)), "=ISFORMULA(A3)");
        Assert.Equal("{=ROW(B1:B3)}", engine.GetValue(new SheetCell(0, new CellAddress(98, 25))).AsText());
        Assert.True(engine.GetValue(new SheetCell(0, new CellAddress(97, 25))).AsBoolean());
    }

    [Fact]
    public void Le_etichette_trovate_da_sole_valgono_come_nomi_solo_se_l_impostazione_e_attiva()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Vendite"));
        for (var row = 1; row <= 3; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row * 10));
        engine.RecalculateAll();
        var target = new SheetCell(0, new CellAddress(98, 25));

        engine.SetFormula(target, "=SUM(Vendite)");
        Assert.Equal(CellError.Name, engine.GetValue(target).Error);

        workbook.Calculation = workbook.Calculation with { AutomaticFindLabels = true };
        engine.SetFormula(target, "=SUM(Vendite)");
        Assert.Equal(60, engine.GetValue(target).AsNumber());
    }

    [Fact]
    public void Gli_intervalli_di_etichette_definiti_valgono_come_nomi()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Nord"));
        sheet.SetValue(new CellAddress(0, 2), CellValue.Text("Sud"));
        for (var row = 1; row <= 3; row++)
        {
            sheet.SetValue(new CellAddress(row, 1), CellValue.Number(row));
            sheet.SetValue(new CellAddress(row, 2), CellValue.Number(row * 100));
        }
        sheet.Labels.Add(new LabelRange(new CellRange(new CellAddress(0, 1), new CellAddress(0, 2)), new CellRange(new CellAddress(1, 1), new CellAddress(3, 2)), LabelOrientation.Columns));
        engine.RecalculateAll();
        var target = new SheetCell(0, new CellAddress(98, 25));
        engine.SetFormula(target, "=SUM(Sud)");
        Assert.Equal(600, engine.GetValue(target).AsNumber());
    }
}

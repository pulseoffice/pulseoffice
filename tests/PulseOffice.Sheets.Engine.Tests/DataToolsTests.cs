using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DataToolsTests
{
    private static Worksheet SheetWith(params object?[][] rows)
    {
        var sheet = new Worksheet("Sheet1");

        for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < rows[row].Length; column++)
                sheet.SetValue(new CellAddress(row, column), rows[row][column] switch
                {
                    null => CellValue.Empty,
                    int number => CellValue.Number(number),
                    double number => CellValue.Number(number),
                    var text => CellValue.Text(text.ToString()!),
                });

        return sheet;
    }

    private static CellRange Range(int firstRow, int firstColumn, int lastRow, int lastColumn) =>
        new(new CellAddress(firstRow, firstColumn), new CellAddress(lastRow, lastColumn));

    [Fact]
    public void LeRigheDuplicateSiRiconosconoSuTutteLeColonne()
    {
        var sheet = SheetWith(
            ["Nome", "Città"],
            ["Ada", "Torino"],
            ["Ada", "Milano"],
            ["Ada", "Torino"]);

        var plan = new DuplicateRemover(new SelectionWorkArea()).Plan(sheet, new DuplicateRemovalRequest(Range(0, 0, 3, 1)));

        Assert.Equal([3], plan.RowsToRemove);
        Assert.Equal(2, plan.Kept);
    }

    [Fact]
    public void SiPuoGuardareSoloUnaColonnaChiave()
    {
        var sheet = SheetWith(
            ["Nome", "Città"],
            ["Ada", "Torino"],
            ["Ada", "Milano"]);

        var plan = new DuplicateRemover(new SelectionWorkArea()).Plan(
            sheet, new DuplicateRemovalRequest(Range(0, 0, 2, 1)) { KeyColumns = [0] });

        Assert.Equal([2], plan.RowsToRemove);
    }

    [Fact]
    public void SenzaIntestazioneAncheLaPrimaRigaConta()
    {
        var sheet = SheetWith(["Ada"], ["Ada"]);

        var plan = new DuplicateRemover(new SelectionWorkArea()).Plan(
            sheet, new DuplicateRemovalRequest(Range(0, 0, 1, 0), HasHeader: false));

        Assert.Single(plan.RowsToRemove);
    }

    [Fact]
    public void IlTestoSiDivideSuiSeparatori()
    {
        var parts = new TextToColumns(new PulseOffice.Foundation.Text.DelimitedText()).Split("a;b;c", new TextToColumnsRequest(Range(0, 0, 0, 0)));

        Assert.Equal(["a", "b", "c"], parts);
    }

    [Fact]
    public void QuelloFraVirgoletteRestaUnito()
    {
        var parts = new TextToColumns(new PulseOffice.Foundation.Text.DelimitedText()).Split(
            "\"Rossi; Ada\";Torino", new TextToColumnsRequest(Range(0, 0, 0, 0)));

        Assert.Equal(["Rossi; Ada", "Torino"], parts);
    }

    [Fact]
    public void LeVirgoletteRaddoppiateSonoUnaVirgoletta()
    {
        var parts = new TextToColumns(new PulseOffice.Foundation.Text.DelimitedText()).Split(
            "\"Detto \"\"Ada\"\"\";x", new TextToColumnsRequest(Range(0, 0, 0, 0)));

        Assert.Equal("Detto \"Ada\"", parts[0]);
    }

    [Fact]
    public void INumeriRiconosciutiDiventanoNumeri()
    {
        var sheet = SheetWith(["10;20"]);

        var plan = new TextToColumns(new PulseOffice.Foundation.Text.DelimitedText()).Plan(sheet, new TextToColumnsRequest(Range(0, 0, 0, 0)));

        Assert.Equal(10, plan.Values[new CellAddress(0, 0)].AsNumber());
        Assert.Equal(20, plan.Values[new CellAddress(0, 1)].AsNumber());
        Assert.Equal(2, plan.WidestRow);
    }

    [Fact]
    public void ISubtotaliNonDistinguonoMaiuscoleComeCalc()
    {
        var sheet = SheetWith(
            ["Regione", "Importo"],
            ["Nord", 10],
            ["nord", 20],
            ["Sud", 5]);

        var plan = new SheetSubtotals().Plan(
            sheet, new SubtotalRequest(Range(0, 0, 3, 1), GroupColumn: 0, ValueColumns: [1]) { GrandTotal = false });

        Assert.Equal(2, plan.Rows.Count);
        Assert.Equal("=SUBTOTAL(9;B2:B3)", plan.Rows[0].Formulas[1]);
    }

    [Fact]
    public void ISubtotaliSiFermanoAOgniCambioDiGruppo()
    {
        var sheet = SheetWith(
            ["Regione", "Importo"],
            ["Nord", 10],
            ["Nord", 20],
            ["Sud", 5]);

        var plan = new SheetSubtotals().Plan(
            sheet,
            new SubtotalRequest(Range(0, 0, 3, 1), GroupColumn: 0, ValueColumns: [1])
            {
                FunctionLabel = "Somma",
                GrandTotalLabel = "Totale generale",
            });

        Assert.Equal(3, plan.Rows.Count);
        Assert.Equal("Nord - Somma", plan.Rows[0].Label);
        Assert.Equal("=SUBTOTAL(9;B2:B3)", plan.Rows[0].Formulas[1]);
        Assert.Equal("=SUBTOTAL(9;B2:B4)", plan.Rows[2].Formulas[1]);
    }

    [Fact]
    public void ConsolidarePerPosizioneSommaCellaSuCella()
    {
        var workbook = Workbook.CreateDefault();
        var first = workbook.Sheets[0];
        var second = workbook.AddSheet("Secondo");

        first.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        first.SetValue(new CellAddress(1, 0), CellValue.Number(2));
        second.SetValue(new CellAddress(0, 0), CellValue.Number(10));
        second.SetValue(new CellAddress(1, 0), CellValue.Number(20));

        var plan = new SheetConsolidation().Plan(workbook, new ConsolidationRequest(
            [new ConsolidationSource(0, Range(0, 0, 1, 0)), new ConsolidationSource(1, Range(0, 0, 1, 0))],
            new CellAddress(0, 3)));

        Assert.Equal(11, plan.Values[new CellAddress(0, 3)].AsNumber());
        Assert.Equal(22, plan.Values[new CellAddress(1, 3)].AsNumber());
    }

    [Fact]
    public void ConsolidarePerEtichettaAllineaVociDiverse()
    {
        var workbook = Workbook.CreateDefault();
        var first = workbook.Sheets[0];
        var second = workbook.AddSheet("Secondo");

        first.SetValue(new CellAddress(0, 0), CellValue.Text("Vite"));
        first.SetValue(new CellAddress(0, 1), CellValue.Number(5));
        first.SetValue(new CellAddress(1, 0), CellValue.Text("Dado"));
        first.SetValue(new CellAddress(1, 1), CellValue.Number(7));

        second.SetValue(new CellAddress(0, 0), CellValue.Text("Dado"));
        second.SetValue(new CellAddress(0, 1), CellValue.Number(3));

        var plan = new SheetConsolidation().Plan(workbook, new ConsolidationRequest(
            [new ConsolidationSource(0, Range(0, 0, 1, 1)), new ConsolidationSource(1, Range(0, 0, 0, 1))],
            new CellAddress(5, 0))
        {
            LabelsInFirstColumn = true,
        });

        Assert.Equal("Vite", plan.Values[new CellAddress(5, 0)].AsText());
        Assert.Equal("Dado", plan.Values[new CellAddress(6, 0)].AsText());
        Assert.Equal(5, plan.Values[new CellAddress(5, 1)].AsNumber());
        Assert.Equal(10, plan.Values[new CellAddress(6, 1)].AsNumber());
    }

    [Fact]
    public void IlConsolidamentoPerEtichettaNonDistingueMaiuscole()
    {
        var workbook = Workbook.CreateDefault();
        var first = workbook.Sheets[0];
        var second = workbook.AddSheet("Secondo");

        first.SetValue(new CellAddress(0, 0), CellValue.Text("Dado"));
        first.SetValue(new CellAddress(0, 1), CellValue.Number(7));
        second.SetValue(new CellAddress(0, 0), CellValue.Text("DADO"));
        second.SetValue(new CellAddress(0, 1), CellValue.Number(3));

        var plan = new SheetConsolidation().Plan(workbook, new ConsolidationRequest(
            [new ConsolidationSource(0, Range(0, 0, 0, 1)), new ConsolidationSource(1, Range(0, 0, 0, 1))],
            new CellAddress(5, 0))
        {
            LabelsInFirstColumn = true,
        });

        Assert.Equal("Dado", plan.Values[new CellAddress(5, 0)].AsText());
        Assert.Equal(10, plan.Values[new CellAddress(5, 1)].AsNumber());
        Assert.False(plan.Values.ContainsKey(new CellAddress(6, 0)));
    }

    [Fact]
    public void IlTestoInColonneNonLeggeSeparatoriDelleMigliaiaNeZeriIniziali()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("it-IT");

        try
        {
            var sheet = SheetWith(["1.000;007;(5);1,5"]);

            var plan = new TextToColumns(new PulseOffice.Foundation.Text.DelimitedText()).Plan(sheet, new TextToColumnsRequest(Range(0, 0, 0, 0)));

            Assert.Equal(ValueKind.Text, plan.Values[new CellAddress(0, 0)].Kind);
            Assert.Equal("007", plan.Values[new CellAddress(0, 1)].AsText());
            Assert.Equal(ValueKind.Text, plan.Values[new CellAddress(0, 2)].Kind);
            Assert.Equal(1.5, plan.Values[new CellAddress(0, 3)].AsNumber());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ConPiuSeparatoriLeVirgoletteRestanoUnite()
    {
        var parts = new TextToColumns(new PulseOffice.Foundation.Text.DelimitedText()).Split(
            "\"a,b;c\",d;e", new TextToColumnsRequest(Range(0, 0, 0, 0)) { Separators = [';', ','] });

        Assert.Equal(["a,b;c", "d", "e"], parts);
    }

    [Fact]
    public void IDuplicatiDistinguonoIlTipoDelValore()
    {
        var sheet = SheetWith(["1"], [1]);

        var plan = new DuplicateRemover(new SelectionWorkArea()).Plan(sheet, new DuplicateRemovalRequest(Range(0, 0, 1, 0), HasHeader: false));

        Assert.Empty(plan.RowsToRemove);
    }

    [Fact]
    public void IlConsolidamentoCalcolaLaDeviazioneStandardEContaLeCellePiene()
    {
        var workbook = Workbook.CreateDefault();
        var first = workbook.Sheets[0];
        var second = workbook.AddSheet("Secondo");

        first.SetValue(new CellAddress(0, 0), CellValue.Number(2));
        second.SetValue(new CellAddress(0, 0), CellValue.Number(4));
        first.SetValue(new CellAddress(1, 0), CellValue.Text("x"));
        second.SetValue(new CellAddress(1, 0), CellValue.Number(1));

        ConsolidationRequest Request(SubtotalFunction function) => new(
            [new ConsolidationSource(0, Range(0, 0, 1, 0)), new ConsolidationSource(1, Range(0, 0, 1, 0))],
            new CellAddress(0, 3)) { Function = function };

        var spread = new SheetConsolidation().Plan(workbook, Request(SubtotalFunction.StandardDeviation));
        var count = new SheetConsolidation().Plan(workbook, Request(SubtotalFunction.Count));
        var numbers = new SheetConsolidation().Plan(workbook, Request(SubtotalFunction.CountNumbers));

        Assert.Equal(Math.Sqrt(2), spread.Values[new CellAddress(0, 3)].AsNumber(), 6);
        Assert.Equal(2, count.Values[new CellAddress(1, 3)].AsNumber());
        Assert.Equal(1, numbers.Values[new CellAddress(1, 3)].AsNumber());
    }

    [Fact]
    public void ConsolidarePerEtichettaDiColonnaNonSpostaLeColonne()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Vite"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(5));

        var plan = new SheetConsolidation().Plan(workbook, new ConsolidationRequest(
            [new ConsolidationSource(0, Range(0, 0, 1, 0))], new CellAddress(5, 0))
        {
            LabelsInFirstRow = true,
        });

        Assert.Equal("Vite", plan.Values[new CellAddress(5, 0)].AsText());
        Assert.Equal(5, plan.Values[new CellAddress(6, 0)].AsNumber());
    }
}

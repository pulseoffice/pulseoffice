using PulseOffice.Document.Commands;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class FormulaTranslatorTests
{
    [Theory]
    [InlineData("=A1+B1*2", "=A2+B2*2")]
    [InlineData("=(A1+B1)*2", "=(A2+B2)*2")]
    [InlineData("=A1-(B1-C1)", "=A2-(B2-C2)")]
    [InlineData("=A1-B1-C1", "=A2-B2-C2")]
    [InlineData("=A1/(B1*C1)", "=A2/(B2*C2)")]
    [InlineData("=A1^B1^C1", "=A2^B2^C2")]
    [InlineData("=(A1^B1)^C1", "=A2^B2^C2")]
    [InlineData("=A1^(B1^C1)", "=A2^(B2^C2)")]
    [InlineData("=-(A1+B1)", "=-(A2+B2)")]
    [InlineData("=(A1+B1)%", "=(A2+B2)%")]
    public void LaFormaDellaFormulaSiConservaTraslandola(string formula, string expected) =>
    Assert.Equal(expected, new FormulaTranslator().Translate(formula, 1, 0));

    [Theory]
    [InlineData("=A1", 1, 0, "=A2")]
    [InlineData("=A1", 0, 1, "=B1")]
    [InlineData("=A1+B1", 1, 0, "=A2+B2")]
    [InlineData("=SUM(A1:A3)", 2, 0, "=SUM(A3:A5)")]
    public void IRiferimentiRelativiSiTraslano(string formula, int rows, int columns, string expected) =>
        Assert.Equal(expected, new FormulaTranslator().Translate(formula, rows, columns));

    [Theory]
    [InlineData("=$A$1", 5, 5, "=$A$1")]
    [InlineData("=$A1", 2, 3, "=$A3")]
    [InlineData("=A$1", 2, 3, "=D$1")]
    public void IlDollaroBloccaLaComponente(string formula, int rows, int columns, string expected) =>

        Assert.Equal(expected, new FormulaTranslator().Translate(formula, rows, columns));

    [Fact]
    public void UnaTraslazioneFuoriDalFoglioDiventaRiferimentoRotto()
    {
        Assert.Equal("=#REF!", new FormulaTranslator().Translate("=A1", -1, 0));
    }

    [Fact]
    public void SenzaScostamentoLaFormulaNonCambia() =>
        Assert.Equal("=SUM(A1:A3)", new FormulaTranslator().Translate("=SUM(A1:A3)", 0, 0));

    [Fact]
    public void IlTestoDentroLaFormulaNonViaeneToccato()
    {
        var translated = new FormulaTranslator().Translate("=IF(A1;\"A1\";\"\")", 1, 0);

        Assert.Contains("A2", translated, StringComparison.Ordinal);
        Assert.Contains("\"A1\"", translated, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaFormulaMalformataNonSiTraduce() =>
        Assert.Null(new FormulaTranslator().Translate("=1+*2", 1, 0));
}

public class SheetClipboardTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Worksheet SheetWith(params (string Address, double Value)[] values)
    {
        var sheet = new Worksheet("Foglio1");
        foreach (var (address, value) in values) sheet.SetValue(A(address), CellValue.Number(value));
        return sheet;
    }

    [Fact]
    public void ICopiaEIncollaRiportanoIValori()
    {
        var sheet = SheetWith(("A1", 1), ("A2", 2), ("A3", 3));
        var snapshot = new SheetClipboard().Copy(sheet, new CellRange(A("A1"), A("A3")));

        new SheetClipboard().Paste(snapshot, sheet, A("C1"), A("A1"));

        Assert.Equal(1, sheet[A("C1")].Value.AsNumber());
        Assert.Equal(3, sheet[A("C3")].Value.AsNumber());
    }

    [Fact]
    public void LeFormuleSiTraslanoIncollando()
    {
        var sheet = SheetWith(("A1", 10), ("B1", 20));
        sheet.SetFormula(A("C1"), "=A1+B1");

        var snapshot = new SheetClipboard().Copy(sheet, CellRange.Single(A("C1")));
        new SheetClipboard().Paste(snapshot, sheet, A("C2"), A("C1"));

        Assert.Equal("=A2+B2", sheet[A("C2")].Formula);
    }

    [Fact]
    public void IncollareSoloIValoriTrasformaLaFormulaInNumero()
    {
        var sheet = new Worksheet("Foglio1");
        sheet[A("A1")] = new Cell { Formula = "=1+1", Value = CellValue.Number(2) };

        var snapshot = new SheetClipboard().Copy(sheet, CellRange.Single(A("A1")));
        new SheetClipboard().Paste(snapshot, sheet, A("B1"), A("A1"), PasteParts.ValuesOnly);

        Assert.Null(sheet[A("B1")].Formula);
        Assert.Equal(2, sheet[A("B1")].Value.AsNumber());
    }

    [Fact]
    public void IncollareSoloGliStiliLasciaIlContenuto()
    {
        var sheet = SheetWith(("A1", 1), ("B1", 99));
        sheet.SetStyle(A("A1"), 7);

        var snapshot = new SheetClipboard().Copy(sheet, CellRange.Single(A("A1")));
        new SheetClipboard().Paste(snapshot, sheet, A("B1"), A("A1"), PasteParts.StylesOnly);

        Assert.Equal(7, sheet[A("B1")].StyleId);
        Assert.Equal(99, sheet[A("B1")].Value.AsNumber());
    }

    [Fact]
    public void LIncollaSiAnnullaInUnColpoSolo()
    {
        var sheet = SheetWith(("A1", 1), ("A2", 2));
        var history = new UndoStack();

        var snapshot = new SheetClipboard().Copy(sheet, new CellRange(A("A1"), A("A2")));
        new SheetClipboard().Paste(snapshot, sheet, A("C1"), A("A1"), PasteParts.All, history);

        history.Undo();

        Assert.True(sheet[A("C1")].Value.IsEmpty);
        Assert.True(sheet[A("C2")].Value.IsEmpty);
    }

    [Fact]
    public void ITestoDelimitatoVaEVieneDagliAppuntiDiSistema()
    {
        var sheet = SheetWith(("A1", 1), ("B1", 2));
        var snapshot = new SheetClipboard().Copy(sheet, new CellRange(A("A1"), A("B1")));

        var text = new SheetClipboard().ToDelimitedText(snapshot);
        Assert.Equal("1\t2", text.TrimEnd());

        var parsed = new SheetClipboard().FromDelimitedText("10\tciao\n20\tmondo");
        Assert.Equal(2, parsed.RowCount);
        Assert.Equal(2, parsed.ColumnCount);

        Assert.Contains(parsed.Cells, c => c.Cell.Value.Kind == ValueKind.Number);
        Assert.Contains(parsed.Cells, c => c.Cell.Value.Kind == ValueKind.Text);
    }
}

public class TabularClipboardTests
{
    [Fact]
    public void UnaCellaSuPiuRigheOConTabulazioneAttraversaGliAppuntiIntera()
    {
        var sheet = new Worksheet("Foglio");
        sheet[new CellAddress(0, 0)] = new Cell { Value = CellValue.Text("riga uno\nriga due") };
        sheet[new CellAddress(0, 1)] = new Cell { Value = CellValue.Text("a\tb \"c\"") };
        sheet[new CellAddress(1, 0)] = new Cell { Value = CellValue.Number(3) };
        var clipboard = new SheetClipboard();

        var text = clipboard.ToDelimitedText(clipboard.Copy(sheet, new CellRange(new CellAddress(0, 0), new CellAddress(1, 1))));
        var back = clipboard.FromDelimitedText(text);

        Assert.Equal(2, back.RowCount);
        Assert.Equal(2, back.ColumnCount);
        Assert.Equal("riga uno\nriga due", back.Cells.Single(c => c.Row == 0 && c.Column == 0).Cell.Value.ToString());
        Assert.Equal("a\tb \"c\"", back.Cells.Single(c => c.Row == 0 && c.Column == 1).Cell.Value.ToString());
        Assert.Equal(ValueKind.Number, back.Cells.Single(c => c.Row == 1).Cell.Value.Kind);
    }
}

public class SheetSearchTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Worksheet Sheet()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(A("A1"), CellValue.Text("Mela"));
        sheet.SetValue(A("A2"), CellValue.Text("melone"));
        sheet.SetValue(A("A3"), CellValue.Text("Pera"));
        sheet.SetValue(A("B1"), CellValue.Number(10));
        sheet[A("B2")] = new Cell { Formula = "=A1&\"x\"", Value = CellValue.Text("Melax") };
        return sheet;
    }

    [Fact]
    public void LaRicercaIgnoraLeMaiuscolePerPredefinito()
    {
        var matches = new SheetSearch().FindAll(Sheet(), "mel");
        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void ConLaDistinzioneDelleMaiuscoleSiRestringe()
    {
        var matches = new SheetSearch().FindAll(Sheet(), "Mel", new SearchOptions { MatchCase = true });
        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void CellaInteraRichiedeCorrispondenzaCompleta()
    {
        Assert.Empty(new SheetSearch().FindAll(Sheet(), "Mel", new SearchOptions { WholeCell = true }));
        Assert.Single(new SheetSearch().FindAll(Sheet(), "Mela", new SearchOptions { WholeCell = true }));
    }

    [Fact]
    public void CercareNelleFormuleEUnaRicercaDiversa()
    {
        var sheet = Sheet();

        Assert.Empty(new SheetSearch().FindAll(sheet, "A1"));
        Assert.Single(new SheetSearch().FindAll(sheet, "A1", new SearchOptions { SearchFormulas = true }));
    }

    [Fact]
    public void LaSostituzioneRiportaQuanteCelleSonoCambiate()
    {
        var sheet = Sheet();
        var replaced = new SheetSearch().ReplaceAll(sheet, "Mel", "Pesc");

        Assert.Equal(2, replaced);
        Assert.Equal("Pesca", sheet[A("A1")].Value.AsText());
        Assert.Equal("=A1&\"x\"", sheet[A("B2")].Formula);
    }

    [Fact]
    public void LaSostituzioneSiAnnulla()
    {
        var sheet = Sheet();
        var history = new UndoStack();

        new SheetSearch().ReplaceAll(sheet, "Mel", "Pesc", history: history);
        history.Undo();

        Assert.Equal("Mela", sheet[A("A1")].Value.AsText());
    }

    [Fact]
    public void UnEspressioneRegolareNonValidaNonTrovaNulla() =>
        Assert.Empty(new SheetSearch().FindAll(Sheet(), "[", new SearchOptions { UseRegex = true }));

    [Fact]
    public void LaSostituzioneCheProduceUnNumeroDaUnNumero()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(A("A1"), CellValue.Text("10"));

        new SheetSearch().ReplaceAll(sheet, "10", "20");

        Assert.Equal(ValueKind.Number, sheet[A("A1")].Value.Kind);
    }
}

public class SheetSortTests
{
    private static readonly ISheetSort Sorter = new SheetSorter();

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Worksheet Sheet()
    {
        var sheet = new Worksheet("Foglio1");
        string[] nomi = ["Carlo", "Anna", "Bruno"];
        double[] valori = [30, 10, 20];

        for (var i = 0; i < 3; i++)
        {
            sheet.SetValue(new CellAddress(i, 0), CellValue.Text(nomi[i]));
            sheet.SetValue(new CellAddress(i, 1), CellValue.Number(valori[i]));
        }

        return sheet;
    }

    [Fact]
    public void OrdinaPerLaColonnaIndicata()
    {
        var sheet = Sheet();
        Sorter.Sort(sheet, new CellRange(A("A1"), A("B3")), [new SortKey(0)]);

        Assert.Equal("Anna", sheet[A("A1")].Value.AsText());
        Assert.Equal("Bruno", sheet[A("A2")].Value.AsText());
        Assert.Equal("Carlo", sheet[A("A3")].Value.AsText());
    }

    [Fact]
    public void LeRigheRestanoIntegre()
    {
        var sheet = Sheet();
        Sorter.Sort(sheet, new CellRange(A("A1"), A("B3")), [new SortKey(0)]);

        Assert.Equal(10, sheet[A("B1")].Value.AsNumber());
        Assert.Equal(20, sheet[A("B2")].Value.AsNumber());
        Assert.Equal(30, sheet[A("B3")].Value.AsNumber());
    }

    [Fact]
    public void InOrdineDecrescenteLeCelleVuoteRestanoInFondo()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(A("A1"), CellValue.Number(3));
        sheet.SetValue(A("A3"), CellValue.Number(1));
        sheet.SetValue(A("B2"), CellValue.Text("vuota"));

        Sorter.Sort(sheet, new CellRange(A("A1"), A("B3")), [new SortKey(0, Ascending: false)]);

        Assert.Equal(3, sheet[A("A1")].Value.AsNumber());
        Assert.Equal(1, sheet[A("A2")].Value.AsNumber());
        Assert.Equal("vuota", sheet[A("B3")].Value.AsText());
    }

    [Fact]
    public void LOrdineDecrescenteSiRovescia()
    {
        var sheet = Sheet();
        Sorter.Sort(sheet, new CellRange(A("A1"), A("B3")), [new SortKey(1, Ascending: false)]);

        Assert.Equal(30, sheet[A("B1")].Value.AsNumber());
    }

    [Fact]
    public void LIntestazioneRestaAlSuoPosto()
    {
        var sheet = Sheet();
        sheet.SetValue(A("A1"), CellValue.Text("Nome"));

        Sorter.Sort(sheet, new CellRange(A("A1"), A("B3")), [new SortKey(0)], hasHeader: true);

        Assert.Equal("Nome", sheet[A("A1")].Value.AsText());
    }

    [Fact]
    public void UnOrdinamentoSenzaEffettoNonEntraNellaCronologia()
    {
        var sheet = Sheet();
        var history = new UndoStack();

        Sorter.Sort(sheet, new CellRange(A("A1"), A("B3")), [new SortKey(0)], history: history);
        Sorter.Sort(sheet, new CellRange(A("A1"), A("B3")), [new SortKey(0)], history: history);

        history.Undo();
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void LOrdinamentoSiAnnulla()
    {
        var sheet = Sheet();
        var history = new UndoStack();

        Sorter.Sort(sheet, new CellRange(A("A1"), A("B3")), [new SortKey(0)], history: history);
        history.Undo();

        Assert.Equal("Carlo", sheet[A("A1")].Value.AsText());
    }
}

public class SheetFilterTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Worksheet Sheet()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(A("A1"), CellValue.Text("Prodotto"));
        sheet.SetValue(A("B1"), CellValue.Text("Quantità"));

        string[] prodotti = ["Mele", "Pere", "Mele"];
        double[] quantita = [10, 5, 30];

        for (var i = 0; i < 3; i++)
        {
            sheet.SetValue(new CellAddress(i + 1, 0), CellValue.Text(prodotti[i]));
            sheet.SetValue(new CellAddress(i + 1, 1), CellValue.Number(quantita[i]));
        }

        return sheet;
    }

    [Fact]
    public void IlFiltroPerElencoNascondeCioCheNonEAmmesso()
    {
        var hidden = new SheetFilterService().RowsToHide(Sheet(), new CellRange(A("A1"), A("B4")),
            [new ColumnFilter(0) { AllowedValues = new HashSet<string> { "Mele" } }]);

        Assert.Single(hidden);
        Assert.Contains(2, hidden);
    }

    [Fact]
    public void IlCriterioUsaLaStessaSintassiDiContaSe()
    {
        var hidden = new SheetFilterService().RowsToHide(Sheet(), new CellRange(A("A1"), A("B4")),
            [new ColumnFilter(1) { Criteria = ">9" }]);

        Assert.Single(hidden);
    }

    [Fact]
    public void IFiltriSuColonneDiverseSiCombinanoInAnd()
    {
        var hidden = new SheetFilterService().RowsToHide(Sheet(), new CellRange(A("A1"), A("B4")),
        [
            new ColumnFilter(0) { AllowedValues = new HashSet<string> { "Mele" } },
            new ColumnFilter(1) { Criteria = ">20" },
        ]);

        Assert.Equal(2, hidden.Count);
    }

    [Fact]
    public void SenzaFiltriNonSiNascondeNulla() =>
        Assert.Empty(new SheetFilterService().RowsToHide(Sheet(), new CellRange(A("A1"), A("B4")), []));

    [Fact]
    public void IValoriDistintiServonoAllElencoADiscesa()
    {
        var values = new SheetFilterService().DistinctValues(Sheet(), new CellRange(A("A1"), A("B4")), 0);

        Assert.Equal(["Mele", "Pere"], values);
    }
}

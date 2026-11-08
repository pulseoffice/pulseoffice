using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class PasteSpecialTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    private static Worksheet ConDati()
    {
        var sheet = new Worksheet("Dati");
        sheet.SetValue(A("A1"), CellValue.Number(1));
        sheet.SetValue(A("B1"), CellValue.Number(2));
        sheet.SetValue(A("A2"), CellValue.Number(3));
        sheet.SetValue(A("B2"), CellValue.Number(4));
        return sheet;
    }

    [Fact]
    public void CopiareEIncollareCelleUniteTieneLUnioneEDLaToglieAnnullando()
    {
        var sheet = ConDati();
        sheet.Merges.Add(R("A1:B1"));
        var history = new PulseOffice.Document.Commands.UndoStack();
        var snapshot = new SheetClipboard().Copy(sheet, R("A1:B2"));

        new SheetClipboard().Paste(snapshot, sheet, A("D5"), A("A1"), PasteParts.All, history);

        Assert.Contains(R("D5:E5"), sheet.Merges.All);
        new SheetClipboard().Paste(snapshot, sheet, A("H1"), A("A1"), PasteParts.ValuesOnly, history);
        Assert.DoesNotContain(R("H1:I1"), sheet.Merges.All);
        history.Undo();
        history.Undo();
        Assert.DoesNotContain(R("D5:E5"), sheet.Merges.All);
        Assert.Single(sheet.Merges.All);
    }

    [Fact]
    public void TrasporreScambiaRigheEColonne()
    {
        var sheet = ConDati();
        var snapshot = new SheetClipboard().Copy(sheet, R("A1:B2"));

        new SheetClipboard().Paste(snapshot, sheet, A("D1"), A("A1"), new PasteRequest { Transpose = true });

        Assert.Equal(1d, sheet[A("D1")].Value.AsNumber());
        Assert.Equal(2d, sheet[A("D2")].Value.AsNumber());
        Assert.Equal(3d, sheet[A("E1")].Value.AsNumber());
        Assert.Equal(4d, sheet[A("E2")].Value.AsNumber());
    }

    [Fact]
    public void LOperazioneSiApplicaAiValoriGiaPresenti()
    {
        var sheet = ConDati();
        sheet.SetValue(A("D1"), CellValue.Number(10));

        var snapshot = new SheetClipboard().Copy(sheet, R("A1"));

        new SheetClipboard().Paste(
            snapshot, sheet, A("D1"), A("A1"), new PasteRequest { Operation = PasteOperation.Add });

        Assert.Equal(11d, sheet[A("D1")].Value.AsNumber());
    }

    [Fact]
    public void DividerePerZeroDaErroreInveceDiInfinito()
    {
        var sheet = ConDati();
        sheet.SetValue(A("A1"), CellValue.Number(0));
        sheet.SetValue(A("D1"), CellValue.Number(10));

        var snapshot = new SheetClipboard().Copy(sheet, R("A1"));

        new SheetClipboard().Paste(
            snapshot, sheet, A("D1"), A("A1"), new PasteRequest { Operation = PasteOperation.Divide });

        Assert.Equal(ValueKind.Error, sheet[A("D1")].Value.Kind);
    }

    [Fact]
    public void SaltandoLeCelleVuoteIlContenutoPrecedenteResta()
    {
        var sheet = ConDati();
        sheet[A("A1")] = Cell.Blank;
        sheet.SetValue(A("D1"), CellValue.Text("resto"));

        var snapshot = new SheetClipboard().Copy(sheet, R("A1:B1"));

        new SheetClipboard().Paste(
            snapshot, sheet, A("D1"), A("A1"), new PasteRequest { SkipBlanks = true });

        Assert.Equal("resto", sheet[A("D1")].Value.ToString());
        Assert.Equal(2d, sheet[A("E1")].Value.AsNumber());
    }

    [Fact]
    public void TrasporreSoloIFormatiNonToccaIValori()
    {
        var sheet = ConDati();
        sheet.SetValue(A("D1"), CellValue.Number(99));
        var snapshot = new SheetClipboard().Copy(sheet, R("A1:B2"));

        new SheetClipboard().Paste(snapshot, sheet, A("D1"), A("A1"), new PasteRequest { Transpose = true, Parts = PasteParts.Styles });

        Assert.Equal(99d, sheet[A("D1")].Value.AsNumber());
    }

    [Fact]
    public void TrasporreConservaLeFormuleTraslate()
    {
        var sheet = ConDati();
        sheet.SetFormula(A("B1"), "=A1*2");
        var snapshot = new SheetClipboard().Copy(sheet, R("A1:B1"));

        new SheetClipboard().Paste(snapshot, sheet, A("D1"), A("A1"), new PasteRequest { Transpose = true });

        Assert.Equal("=C2*2", sheet[A("D2")].Formula);
    }

    [Fact]
    public void CommentiEConvalidaSiIncollanoSoloSeChiesti()
    {
        var sheet = ConDati();
        sheet.Comments[A("A1")] = new CellComment { Text = "nota" };
        sheet.Validations.Add(new DataValidation { Range = R("A1:A5"), Kind = ValidationKind.WholeNumber, FirstFormula = "1", SecondFormula = "9" });
        var clipboard = new SheetClipboard();
        var snapshot = clipboard.Copy(sheet, R("A1:B2"));

        Assert.Single(snapshot.Comments);
        Assert.Equal((0, 0, 2, 1), (snapshot.Validations[0].Row, snapshot.Validations[0].Column, snapshot.Validations[0].RowCount, snapshot.Validations[0].ColumnCount));

        clipboard.Paste(snapshot, sheet, A("D1"), A("A1"), new PasteRequest());
        Assert.False(sheet.Comments.ContainsKey(A("D1")));
        Assert.Single(sheet.Validations);

        var history = new PulseOffice.Document.Commands.UndoStack();
        clipboard.Paste(snapshot, sheet, A("D1"), A("A1"), new PasteRequest { Parts = PasteParts.Comments | PasteParts.Validation }, history);
        Assert.Equal("nota", sheet.Comments[A("D1")].Text);
        Assert.Equal(R("D1:D2"), sheet.Validations[^1].Range);
        Assert.Equal(1d, sheet[A("D1")].Value.AsNumber());

        history.Undo();
        Assert.False(sheet.Comments.ContainsKey(A("D1")));
        Assert.Single(sheet.Validations);
    }

    [Fact]
    public void IlCollegamentoScriveUnRiferimentoAllaCellaCopiata()
    {
        var sheet = ConDati();
        var other = new Worksheet("Altro foglio");
        var clipboard = new SheetClipboard();
        var snapshot = clipboard.Copy(sheet, R("A1:B2"));

        clipboard.Paste(snapshot, sheet, A("D4"), A("A1"), new PasteRequest { Link = true });
        clipboard.Paste(snapshot, other, A("A1"), A("A1"), new PasteRequest { Link = true, Transpose = true });

        Assert.Equal("=A1", sheet[A("D4")].Formula);
        Assert.Equal("=B2", sheet[A("E5")].Formula);
        Assert.Equal(4d, sheet[A("E5")].Value.AsNumber());
        Assert.Equal("=Dati.B1", other[A("A2")].Formula);
    }

    [Fact]
    public void IncollareCollegamentiSiAnnullaInUnPassoSoloColNomeDatoDalChiamante()
    {
        var sheet = ConDati();
        var clipboard = new SheetClipboard();
        var snapshot = clipboard.Copy(sheet, R("A1:B2"));
        var history = new PulseOffice.Document.Commands.UndoStack();

        clipboard.Paste(snapshot, sheet, A("D4"), A("A1"), new PasteRequest { Link = true, HistoryLabel = "Paste" }, history);

        Assert.Equal("Paste", history.UndoDescription);
        history.Undo();
        Assert.False(history.CanUndo);
        Assert.Null(sheet[A("D4")].Formula);
        Assert.Null(sheet[A("E5")].Formula);
    }

    [Fact]
    public void CopiareUnaColonnaInteraNonEnumeraUnMilioneDiCelleMaIncollandoSvuotaComunqueLaDestinazione()
    {
        var sheet = ConDati();
        var destination = new Worksheet("Destinazione");
        destination.SetValue(A("D500"), CellValue.Number(9));
        destination.SetValue(A("D900000"), CellValue.Number(7));
        var clipboard = new SheetClipboard();

        var snapshot = clipboard.Copy(sheet, new CellRange(A("A1"), new CellAddress(CellAddress.MaxRows - 1, 0)));
        Assert.True(snapshot.Cells.Count < 1000);
        Assert.Equal(CellAddress.MaxRows, snapshot.RowCount);

        clipboard.Paste(snapshot, destination, A("D1"), A("A1"), PasteRequest.All);

        Assert.Equal(1, destination.GetValue(A("D1")).AsNumber());
        Assert.Equal(3, destination.GetValue(A("D2")).AsNumber());
        Assert.True(destination.GetValue(A("D500")).IsEmpty);
        Assert.True(destination.GetValue(A("D900000")).IsEmpty);
    }

    [Fact]
    public void IncollareTestoNaNOInfinityLoTieneComeTesto()
    {
        var snapshot = new SheetClipboard().FromDelimitedText("NaN\tInfinity\t-Infinity");
        Assert.All(snapshot.Cells, entry => Assert.Equal(ValueKind.Text, entry.Cell.Value.Kind));
    }
}

using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.UI.DataDialogs;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class DataDialogModelsTests
{
    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public void Una_riga_di_testo_sopra_ai_numeri_e_un_intestazione()
    {
        var sheet = new Worksheet("Dati");
        sheet.SetValue(A("A1"), CellValue.Text("Nome"));
        sheet.SetValue(A("B1"), CellValue.Text("Totale"));
        sheet.SetValue(A("A2"), CellValue.Text("Bruno"));
        sheet.SetValue(A("B2"), CellValue.Number(4));
        var detector = new HeaderRowDetector();

        Assert.True(detector.LooksLikeHeader(sheet, R("A1:B2")));
        Assert.False(detector.LooksLikeHeader(sheet, R("A2:B2")));
        sheet.SetValue(A("B1"), CellValue.Number(1));
        Assert.False(detector.LooksLikeHeader(sheet, R("A1:B2")));
    }

    [Fact]
    public void Gli_ordini_personalizzati_seguono_la_lingua_e_partono_dal_lunedi()
    {
        var orders = new CultureSortOrders().For(CultureInfo.GetCultureInfo("it-IT"));
        Assert.Equal(4, orders.Count);
        Assert.Equal("lunedì", orders[1].Items[0]);
        Assert.Equal("domenica", orders[1].Items[^1]);
        Assert.Equal(12, orders[3].Items.Count);
        Assert.StartsWith("lun, mar, mer", orders[0].Label, StringComparison.Ordinal);
    }

    [Fact]
    public void La_scelta_di_incolla_speciale_diventa_la_richiesta_del_motore()
    {
        Assert.Equal(PasteParts.Everything, new PasteSpecialChoice().Request().Parts);
        Assert.Equal(PasteParts.Formulas | PasteParts.Values, new PasteSpecialChoice(PastePart.Formulas).Request().Parts);
        Assert.Equal(PasteOperation.None, new PasteSpecialChoice(PastePart.Comments, PasteOperation.Add).Request().Operation);
        Assert.Equal(PasteOperation.Divide, new PasteSpecialChoice(PastePart.Values, PasteOperation.Divide).Request().Operation);

        var sketch = new PasteSketcher().Sketch(new PasteSpecialChoice(PastePart.Formats));
        Assert.Equal("2", sketch.Result[1][0].Text);
        Assert.True(sketch.Result[1][0].Formatted);
        Assert.False(sketch.Result[1][1].Comment);
    }
}

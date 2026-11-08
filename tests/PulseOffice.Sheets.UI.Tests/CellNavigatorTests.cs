using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class CellNavigatorTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public void LeRigheNascosteSiSaltano()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.Layout.HideRow(1);
        sheet.Layout.HideRow(2);

        Assert.Equal(A("A4"), new CellNavigator().Step(sheet, A("A1"), 1, 0));
        Assert.Equal(A("A1"), new CellNavigator().Step(sheet, A("A4"), -1, 0));
    }

    [Fact]
    public void LeColonneNascosteSiSaltano()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.Layout.HideColumn(1);

        Assert.Equal(A("C1"), new CellNavigator().Step(sheet, A("A1"), 0, 1));
    }

    [Fact]
    public void UnaRegioneUnitaSiAttraversaInUnPassoSolo()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.Merges.Add(new CellRange(A("B1"), A("D2")));

        var navigator = new CellNavigator();

        Assert.Equal(A("B1"), navigator.Step(sheet, A("A1"), 0, 1));
        Assert.Equal(A("E1"), navigator.Step(sheet, A("B1"), 0, 1));
        Assert.Equal(A("B3"), navigator.Step(sheet, A("B1"), 1, 0));
    }

    [Fact]
    public void AlBordoDelFoglioNonSiEsce()
    {
        var sheet = new Worksheet("Foglio1");

        Assert.Equal(A("A1"), new CellNavigator().Step(sheet, A("A1"), -1, 0));
    }

    [Fact]
    public void IlFormatoDiPiuCelleCambiaSoloCioCheSiEModificato()
    {
        var bold = CellStyle.Default with { Font = CellFont.Default with { Bold = true } };
        var italicRed = CellStyle.Default with
        {
            Font = CellFont.Default with { Italic = true },
            BackgroundHex = "#FF0000",
        };

        var after = bold with { Horizontal = CellHorizontalAlignment.Center };

        var result = new CellStyleDelta().Apply(bold, after, italicRed);

        Assert.Equal(CellHorizontalAlignment.Center, result.Horizontal);
        Assert.True(result.Font.Italic);
        Assert.False(result.Font.Bold);
        Assert.Equal("#FF0000", result.BackgroundHex);
    }


    private static Worksheet Column()
    {
        var sheet = new Worksheet("Foglio1");
        foreach (var a1 in new[] { "A1", "A2", "A3", "A6", "A7" }) sheet.SetValue(A(a1), CellValue.Number(1));
        return sheet;
    }

    [Fact]
    public void IlSaltoVaAllaFineDelBloccoPoiAlBloccoSuccessivoPoiAlBordo()
    {
        var sheet = Column();
        var navigator = new CellNavigator();

        Assert.Equal(A("A3"), navigator.Jump(sheet, A("A1"), 1, 0));
        Assert.Equal(A("A6"), navigator.Jump(sheet, A("A3"), 1, 0));
        Assert.Equal(A("A7"), navigator.Jump(sheet, A("A6"), 1, 0));
        Assert.Equal(new CellAddress(CellAddress.MaxRows - 1, 0), navigator.Jump(sheet, A("A7"), 1, 0));
        Assert.Equal(A("A3"), navigator.Jump(sheet, A("A6"), -1, 0));
        Assert.Equal(A("A1"), navigator.Jump(sheet, A("A3"), -1, 0));
        Assert.Equal(A("A1"), navigator.Jump(sheet, A("A1"), -1, 0));
    }

    [Fact]
    public void FineEUltimaCellaUsata()
    {
        var sheet = Column();
        sheet.SetValue(A("D2"), CellValue.Number(1));
        var navigator = new CellNavigator();

        Assert.Equal(A("D7"), navigator.LastUsed(sheet));
        Assert.Equal(A("D2"), navigator.RowEnd(sheet, A("A2")));
        Assert.Equal(A("B9"), navigator.RowEnd(sheet, A("B9")));
    }
}

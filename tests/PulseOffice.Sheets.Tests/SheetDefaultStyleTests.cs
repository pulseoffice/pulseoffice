using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class SheetDefaultStyleTests
{
    [Fact]
    public void LaCellaVinceSullaRigaCheVinceSullaColonna()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.Layout.SetColumnStyle(2, 5);
        sheet.Layout.SetRowStyle(3, 7);
        sheet.SetStyle(new CellAddress(3, 2), 9);

        Assert.Equal(5, sheet.StyleIdAt(new CellAddress(10, 2)));
        Assert.Equal(7, sheet.StyleIdAt(new CellAddress(3, 8)));
        Assert.Equal(9, sheet.StyleIdAt(new CellAddress(3, 2)));
        Assert.Equal(StyleCatalog.DefaultStyleId, sheet.StyleIdAt(new CellAddress(0, 0)));
    }

    [Fact]
    public void UnaCellaScrittaDopoErediteLoStileDellaColonna()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.Layout.SetColumnStyle(1, 4);

        sheet.SetValue(new CellAddress(500, 1), CellValue.Number(3));

        Assert.Equal(4, sheet[new CellAddress(500, 1)].StyleId);
    }

    [Fact]
    public void LaCopiaDelLayoutPortaGliStili()
    {
        var source = new SheetLayout();
        source.SetColumnStyle(1, 4);
        source.SetRowStyle(2, 6);
        var copy = new SheetLayout();

        source.CopyTo(copy);

        Assert.Equal(4, copy.ColumnStyle(1));
        Assert.Equal(6, copy.RowStyle(2));
    }

    [Fact]
    public void LoStilePredefinitoNonSiMemorizza()
    {
        var layout = new SheetLayout();
        layout.SetColumnStyle(1, StyleCatalog.DefaultStyleId);

        Assert.Empty(layout.ColumnStyles);
    }

    [Fact]
    public void UnaCellaSempliceEsplicitaNonRiprendeLoStileDellaColonna()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.Layout.SetColumnStyle(1, 4);
        var address = new CellAddress(3, 1);

        sheet.SetStyle(address, StyleCatalog.DefaultStyleId);
        sheet.SetValue(address, CellValue.Number(1));

        Assert.Equal(StyleCatalog.DefaultStyleId, sheet.StyleIdAt(address));
        Assert.Equal(StyleCatalog.DefaultStyleId, sheet.OwnStyleAt(address));

        sheet.SetOwnStyle(address, null);
        Assert.Equal(4, sheet.StyleIdAt(address));
        Assert.Null(sheet.OwnStyleAt(address));
    }

    [Fact]
    public void FuoriDaUnaColonnaFormattataLaCellaSempliceNonResta()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetStyle(new CellAddress(2, 2), StyleCatalog.DefaultStyleId);

        Assert.Equal(0, sheet.CellCount);
    }
}

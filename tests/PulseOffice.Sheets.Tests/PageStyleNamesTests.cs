using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class PageStyleNamesTests
{
    [Fact]
    public void UnFoglioChiamatoDefaultNonToccaLoStileCondiviso()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Default");
        workbook.AddSheet("Altro");

        var name = workbook.PageStyles.OwnNameFor(sheet, workbook.Sheets);

        Assert.NotEqual("Default", name, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnoStileCondivisoDaDueFogliNonSiRiusa()
    {
        var workbook = new Workbook();
        var first = workbook.AddSheet("A");
        var second = workbook.AddSheet("B");
        workbook.PageStyles.Define("PageStyle1", workbook.PageStyles.Default.Setup);
        first.PageStyleName = "PageStyle1";
        second.PageStyleName = "PageStyle1";

        Assert.Equal("PageStyle2", workbook.PageStyles.OwnNameFor(first, workbook.Sheets));
    }

    [Fact]
    public void UnoStileProprioSiRiusa()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("A");
        workbook.PageStyles.Define("Mio", workbook.PageStyles.Default.Setup);
        sheet.PageStyleName = "Mio";

        Assert.Equal("Mio", workbook.PageStyles.OwnNameFor(sheet, workbook.Sheets));
    }
}

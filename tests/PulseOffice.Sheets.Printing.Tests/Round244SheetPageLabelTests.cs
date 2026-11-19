using System.Globalization;
using PulseOffice.Foundation.Pages;
using PulseOffice.Printing;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Printing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Printing.Tests;

public sealed class Round244SheetPageLabelTests
{
    [Fact]
    public void The_first_page_number_becomes_the_pdf_page_label()
    {
        var sheet = new Worksheet("Conti");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Nome"));
        var document = new SheetPrintDocument(sheet, new StyleCatalog(),
            new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.MinValue)),
            new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = new PageSetup { Paper = new PaperCatalog().Default, FirstPageNumber = 7 } });

        Assert.Equal(new PrintPageLabel(PageLabelStyle.Decimal, 7), document.PageLabel(0));
        Assert.Null(document.PageLabel(document.PageCount));
    }
}

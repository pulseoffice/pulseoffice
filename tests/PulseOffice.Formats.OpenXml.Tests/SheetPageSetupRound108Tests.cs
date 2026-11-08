using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using Xunit;
using XlsxPageSetup = DocumentFormat.OpenXml.Spreadsheet.PageSetup;

namespace PulseOffice.Formats.OpenXml.Tests;

public class SheetPageSetupRound108Tests
{
    private static Workbook With(PageSetup setup)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.PageStyleName = workbook.PageStyles.Define("Stretta", setup).Name;
        return workbook;
    }

    [Fact]
    public void Fit_to_pages_tall_only_writes_zero_pages_wide()
    {
        var workbook = With(new PageSetup { Paper = new PaperCatalog().Default, FitToHeightPages = 3 });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);

        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var page = package.WorkbookPart!.WorksheetParts.Single().Worksheet!.GetFirstChild<XlsxPageSetup>()!;
            Assert.Equal(0u, page.FitToWidth!.Value);
            Assert.Equal(3u, page.FitToHeight!.Value);
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!;
        var setup = read.PageStyles.For(read.Sheets[0]).Setup;
        Assert.Null(setup.FitToWidthPages);
        Assert.Equal(3, setup.FitToHeightPages);
    }

    [Fact]
    public void A_custom_paper_is_a_declared_loss_in_xlsx()
    {
        var custom = new PaperSize("custom", new DocumentSize(Twip.FromMillimeters(100), Twip.FromMillimeters(150)));
        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(With(new PageSetup { Paper = custom })).Losses,
            loss => loss.Code == "XLSX234");
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(With(new PageSetup { Paper = new PaperCatalog().Default })).Losses,
            loss => loss.Code == "XLSX234");
    }

    [Fact]
    public void Excel_formatting_codes_do_not_become_text_in_ods()
    {
        var workbook = With(new PageSetup
        {
            Paper = new PaperCatalog().Default,
            Header = new HeaderFooter(Center: "&\"Arial,Bold\"&14&BBilancio&B &K0000FF&P"),
        });

        Assert.Contains(new OdsSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "ODF273");

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var read = new OdsReader().Read(stream).Value!;
        Assert.Equal("Bilancio &P", read.PageStyles.For(read.Sheets[0]).Setup.Header.Center);
    }
}

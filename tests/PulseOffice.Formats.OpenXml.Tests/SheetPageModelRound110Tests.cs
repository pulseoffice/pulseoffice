using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Foundation.Pages;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using XlsxHeaderFooter = DocumentFormat.OpenXml.Spreadsheet.HeaderFooter;
using XlsxPageSetup = DocumentFormat.OpenXml.Spreadsheet.PageSetup;

namespace PulseOffice.Formats.OpenXml.Tests;

public class SheetPageModelRound110Tests
{
    private readonly PageSetup _setup = new()
    {
        Paper = new PaperCatalog().Default,
        Header = new HeaderFooter(Center: "Dispari &P"),
        Footer = new HeaderFooter(Right: "&P / &N"),
        DifferentFirstPage = true,
        DifferentOddEven = true,
        FirstHeader = new HeaderFooter(Left: "Copertina"),
        EvenHeader = new HeaderFooter(Center: "Pari &P"),
        EvenFooter = new HeaderFooter(Left: "&A"),
        FirstPageNumber = 5,
    };

    private Workbook With(PageSetup setup)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(new CellAddress(0, 0), Sheets.Values.CellValue.Number(1));
        sheet.PageStyleName = workbook.PageStyles.Define("Varianti", setup).Name;
        return workbook;
    }

    private void AssertVariants(PageSetup read)
    {
        Assert.True(read.DifferentFirstPage);
        Assert.True(read.DifferentOddEven);
        Assert.Equal(_setup.FirstHeader, read.FirstHeader);
        Assert.Equal(HeaderFooter.Empty, read.FirstFooter);
        Assert.Equal(_setup.EvenHeader, read.EvenHeader);
        Assert.Equal(_setup.EvenFooter, read.EvenFooter);
        Assert.Equal(_setup.Header, read.Header);
        Assert.Equal(_setup.Footer, read.Footer);
        Assert.Equal(5, read.FirstPageNumber);
    }

    [Fact]
    public void Xlsx_keeps_first_page_number_and_first_even_headers()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(With(_setup), stream);

        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var worksheet = package.WorkbookPart!.WorksheetParts.Single().Worksheet!;
            var page = worksheet.GetFirstChild<XlsxPageSetup>()!;
            Assert.Equal(5u, page.FirstPageNumber!.Value);
            Assert.True(page.UseFirstPageNumber!.Value);
            var bands = worksheet.GetFirstChild<XlsxHeaderFooter>()!;
            Assert.True(bands.DifferentFirst!.Value);
            Assert.True(bands.DifferentOddEven!.Value);
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!;
        AssertVariants(read.PageStyles.For(read.Sheets[0]).Setup);
    }

    [Fact]
    public void Ods_keeps_first_page_number_and_first_even_headers()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(With(_setup), stream);

        stream.Position = 0;
        var read = new OdsReader().Read(stream).Value!;
        AssertVariants(read.PageStyles.For(read.Sheets[0]).Setup);
    }

    [Fact]
    public void Plain_setup_stays_plain_in_both_formats()
    {
        var plain = new PageSetup { Paper = new PaperCatalog().Default, Header = new HeaderFooter(Center: "&P") };

        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(With(plain), xlsx);
        xlsx.Position = 0;
        var fromXlsx = new XlsxReader().Read(xlsx).Value!;
        var xlsxSetup = fromXlsx.PageStyles.For(fromXlsx.Sheets[0]).Setup;

        using var ods = new MemoryStream();
        new OdsWriter().Write(With(plain), ods);
        ods.Position = 0;
        var fromOds = new OdsReader().Read(ods).Value!;
        var odsSetup = fromOds.PageStyles.For(fromOds.Sheets[0]).Setup;

        foreach (var setup in new[] { xlsxSetup, odsSetup })
        {
            Assert.False(setup.DifferentFirstPage);
            Assert.False(setup.DifferentOddEven);
            Assert.Null(setup.FirstPageNumber);
        }
    }

    [Fact]
    public void Ods_print_range_of_one_cell_is_read()
    {
        var workbook = With(new PageSetup { Paper = new PaperCatalog().Default });
        workbook.Sheets[0].PrintRanges.Add(new CellRange(new CellAddress(2, 1), new CellAddress(2, 1)));

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            entry.Delete();
            var replaced = System.Text.RegularExpressions.Regex.Replace(content, "table:print-ranges=\"[^\"]*\"", "table:print-ranges=\"Dati.$B$3\"");
            Assert.NotEqual(content, replaced);
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(replaced);
        }
        stream.Position = 0;
        var read = new OdsReader().Read(stream).Value!;

        Assert.Equal(new CellRange(new CellAddress(2, 1), new CellAddress(2, 1)), Assert.Single(read.Sheets[0].PrintRanges));
    }
}

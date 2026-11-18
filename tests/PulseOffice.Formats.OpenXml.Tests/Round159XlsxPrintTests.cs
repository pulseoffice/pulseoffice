using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Foundation.Pages;
using PulseOffice.Sheets.Addressing;
using Xunit;
using Book = PulseOffice.Sheets.Workbook;
using XlsxPageSetup = DocumentFormat.OpenXml.Spreadsheet.PageSetup;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round159XlsxPrintTests
{
    private static Book Edited(Action<WorkbookPart, Worksheet> edit)
    {
        var workbook = new Book();
        var sheet = workbook.AddSheet("Dati");
        sheet.PrintRanges.Add(new CellRange(new CellAddress(0, 0), new CellAddress(1, 1)));
        sheet.PageStyleName = workbook.PageStyles.Define("Stretta", new PulseOffice.Foundation.Pages.PageSetup { Paper = new PaperCatalog().Default, FitToHeightPages = 2 }).Name;

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var part = package.WorkbookPart!;
            edit(part, part.WorksheetParts.Single().Worksheet!);
        }

        stream.Position = 0;
        return new XlsxReader().Read(stream).Value!;
    }

    [Fact]
    public void Fit_to_page_without_page_counts_means_one_page_each_way()
    {
        var read = Edited((_, worksheet) =>
        {
            var page = worksheet.GetFirstChild<XlsxPageSetup>()!;
            page.FitToWidth = null;
            page.FitToHeight = null;
        });

        var setup = read.PageStyles.For(read.Sheets[0]).Setup;
        Assert.Equal(1, setup.FitToWidthPages);
        Assert.Equal(1, setup.FitToHeightPages);
    }

    [Fact]
    public void Whole_column_and_whole_row_print_areas_are_read()
    {
        var read = Edited((part, _) =>
            part.Workbook!.DefinedNames!.Elements<DefinedName>().Single(name => name.Name == "_xlnm.Print_Area").Text = "Dati!$B:$C,Dati!$3:$4");

        Assert.Equal(
            [
                new CellRange(new CellAddress(0, 1), new CellAddress(CellAddress.MaxRows - 1, 2)),
                new CellRange(new CellAddress(2, 0), new CellAddress(3, CellAddress.MaxColumns - 1)),
            ],
            read.Sheets[0].PrintRanges);
    }
}

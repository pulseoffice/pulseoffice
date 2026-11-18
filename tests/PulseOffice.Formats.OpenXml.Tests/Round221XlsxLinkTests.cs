using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using Xunit;
using Book = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round221XlsxLinkTests
{
    private static CellAddress A(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    private static MemoryStream Written(Book workbook)
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void A_hyperlink_over_a_range_links_every_cell_of_the_range()
    {
        var workbook = Book.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), PulseOffice.Sheets.Values.CellValue.Text("Site"));
        sheet[A("A1")] = sheet[A("A1")] with { Hyperlink = "https://example.test/" };
        using var stream = Written(workbook);
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var link = package.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<Hyperlink>().Single();
            link.Reference = "A1:B2";
        }
        stream.Position = 0;

        var read = new XlsxReader().Read(stream).Value!.Sheets[0];

        Assert.All(new[] { "A1", "B1", "A2", "B2" }, cell => Assert.Equal("https://example.test/", read[A(cell)].Hyperlink));
        Assert.Null(read[A("C1")].Hyperlink);
    }

    [Fact]
    public void A_hyperlink_screen_tip_crosses_xlsx_and_ods()
    {
        var workbook = Book.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), PulseOffice.Sheets.Values.CellValue.Text("Site"));
        sheet[A("A1")] = sheet[A("A1")] with { Hyperlink = "https://example.test/", HyperlinkTip = "Open the site" };
        sheet.SetValue(A("A2"), PulseOffice.Sheets.Values.CellValue.Number(4));
        sheet[A("A2")] = sheet[A("A2")] with { Hyperlink = "#Sheet1.C3", HyperlinkTip = "Go to the total" };

        using var stream = Written(workbook);
        var fromXlsx = new XlsxReader().Read(stream).Value!;
        using var ods = new MemoryStream();
        new PulseOffice.Formats.Odf.OdsWriter().Write(fromXlsx, ods);
        ods.Position = 0;
        var back = new PulseOffice.Formats.Odf.OdsReader().Read(ods).Value!.Sheets[0];

        Assert.Equal("Open the site", fromXlsx.Sheets[0][A("A1")].HyperlinkTip);
        Assert.Equal("Go to the total", fromXlsx.Sheets[0][A("A2")].HyperlinkTip);
        Assert.Equal("Open the site", back[A("A1")].HyperlinkTip);
        Assert.Equal("Go to the total", back[A("A2")].HyperlinkTip);
    }

    [Theory]
    [InlineData(PulseOffice.Foundation.Pages.CommentPrinting.AtEnd)]
    [InlineData(PulseOffice.Foundation.Pages.CommentPrinting.AsDisplayed)]
    public void Comment_printing_crosses_xlsx_and_ods(PulseOffice.Foundation.Pages.CommentPrinting printing)
    {
        var workbook = Book.CreateDefault();
        workbook.PageStyles.Define("Notes", new PulseOffice.Foundation.Pages.PageSetup { Paper = new PulseOffice.Foundation.Pages.PaperCatalog().Default, PrintComments = printing });
        workbook.Sheets[0].PageStyleName = "Notes";

        using var stream = Written(workbook);
        var fromXlsx = new XlsxReader().Read(stream).Value!;
        using var ods = new MemoryStream();
        new PulseOffice.Formats.Odf.OdsWriter().Write(fromXlsx, ods);
        ods.Position = 0;
        var back = new PulseOffice.Formats.Odf.OdsReader().Read(ods).Value!;

        Assert.Equal(printing, fromXlsx.PageStyles.For(fromXlsx.Sheets[0]).Setup.PrintComments);
        Assert.Equal(printing, back.PageStyles.For(back.Sheets[0]).Setup.PrintComments);
    }

    [Fact]
    public void Writing_cell_comments_to_xlsx_declares_that_their_date_is_lost()
    {
        var workbook = Book.CreateDefault();
        workbook.Sheets[0].Comments[A("B2")] = new PulseOffice.Sheets.CellComment { Text = "Check", Author = "Ada" };

        var loss = Assert.Single(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX243");

        Assert.Contains("1", loss.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_author_heading_excel_puts_at_the_start_of_a_note_is_not_kept_in_the_text()
    {
        var workbook = Book.CreateDefault();
        workbook.Sheets[0].Comments[A("B2")] = new PulseOffice.Sheets.CellComment { Text = "placeholder", Author = "Ada" };
        using var stream = Written(workbook);
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var comments = package.WorkbookPart!.WorksheetParts.Single().WorksheetCommentsPart!.Comments!;
            var text = comments.Descendants<CommentText>().Single();
            text.RemoveAllChildren();
            text.Append(
                new Run(new RunProperties(new Bold()), new DocumentFormat.OpenXml.Spreadsheet.Text("Ada:")),
                new Run(new DocumentFormat.OpenXml.Spreadsheet.Text("\nCheck the total") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }));
            comments.Save();
        }
        stream.Position = 0;

        var comment = new XlsxReader().Read(stream).Value!.Sheets[0].Comments[A("B2")];

        Assert.Equal("Check the total", comment.Text);
        Assert.Equal("Ada", comment.Author);
    }
}

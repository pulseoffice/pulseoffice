using System.Globalization;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Printing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Printing.Tests;

public sealed class Round224CommentPrintTests
{
    private sealed class Recorder : IPageCanvas
    {
        public List<string> Texts { get; } = [];
        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));
        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints((text?.Length ?? 0) * 5.0);
        public void FillRectangle(DocumentRect rect, PrintColor color) { }
        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) { }
        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) => Texts.Add(text);
        public void DrawPath(PrintPath path) { }
        public void DrawImage(PageImage image, DocumentRect bounds) { }
        public void DrawGlyphs(PrintGlyphRun run) => Texts.Add(run.Text);
    }

    private static SheetPrintDocument Document(CommentPrinting printing, bool shown)
    {
        var sheet = new Worksheet("Sheet1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("value"));
        sheet.Comments[new CellAddress(0, 0)] = new CellComment { Text = "remember", Author = "Ada", IsAlwaysVisible = shown };
        return new SheetPrintDocument(sheet, new StyleCatalog(),
            new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.MinValue)),
            new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = new PageSetup { Paper = new PaperCatalog().Default, PrintComments = printing } });
    }

    [Fact]
    public void Comments_at_end_add_a_page_listing_them()
    {
        var document = Document(CommentPrinting.AtEnd, shown: false);
        Assert.Equal(2, document.PageCount);

        var recorder = new Recorder();
        document.PaintPage(1, recorder);

        Assert.Contains("A1", recorder.Texts);
        Assert.Contains(recorder.Texts, text => text.Contains("Ada: remember", StringComparison.Ordinal));
    }

    [Fact]
    public void Comments_as_displayed_print_only_shown_notes_on_their_page()
    {
        var shown = new Recorder();
        Document(CommentPrinting.AsDisplayed, shown: true).PaintPage(0, shown);
        var hidden = new Recorder();
        Document(CommentPrinting.AsDisplayed, shown: false).PaintPage(0, hidden);

        Assert.Contains("Ada: remember", shown.Texts);
        Assert.DoesNotContain("Ada: remember", hidden.Texts);
        Assert.Equal(1, Document(CommentPrinting.None, shown: true).PageCount);
    }
}

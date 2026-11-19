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

public sealed class Round237SheetBookmarkTests
{
    private sealed class Recorder : IPageCanvas
    {
        public List<string> Headings { get; } = [];
        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));
        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints((text?.Length ?? 0) * 5.0);
        public void FillRectangle(DocumentRect rect, PrintColor color) { }
        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) { }
        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) { }
        public void DrawPath(PrintPath path) { }
        public void DrawImage(PageImage image, DocumentRect bounds) { }
        public void DrawGlyphs(PrintGlyphRun run) { }
        public void MarkHeading(int level, string title, double topPoints) => Headings.Add(title);
        public List<string> Links { get; } = [];
        public void MarkLink(DocumentRect area, string target) => Links.Add(target);
    }

    [Fact]
    public void The_first_page_of_a_sheet_marks_its_name_as_a_bookmark()
    {
        var sheet = new Worksheet("Budget");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("value"));
        var document = new SheetPrintDocument(sheet, new StyleCatalog(),
            new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.MinValue)),
            new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = new PageSetup { Paper = new PaperCatalog().Default } });

        var recorder = new Recorder();
        document.PaintPage(0, recorder);

        Assert.Equal(["Budget"], recorder.Headings);
    }

    [Fact]
    public void A_cell_hyperlink_becomes_a_link_in_the_printed_page()
    {
        var sheet = new Worksheet("Links");
        sheet[new CellAddress(0, 0)] = new Cell { Value = CellValue.Text("site"), Hyperlink = "https://example.org/" };
        var document = new SheetPrintDocument(sheet, new StyleCatalog(),
            new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.MinValue)),
            new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = new PageSetup { Paper = new PaperCatalog().Default } });

        var recorder = new Recorder();
        document.PaintPage(0, recorder);

        Assert.Equal(["https://example.org/"], recorder.Links);
    }

    [Fact]
    public void An_object_hyperlink_becomes_a_link_in_the_printed_page()
    {
        var sheet = new Worksheet("Objects");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("x"));
        sheet.Objects.Add(new PlacedObject
        {
            Id = "s1",
            Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "s1", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle },
            Anchor = new CellAddress(1, 0),
            Size = new DocumentSize(Twip.FromPoints(40), Twip.FromPoints(20)),
            Hyperlink = "https://example.org/object",
        });
        var document = new SheetPrintDocument(sheet, new StyleCatalog(),
            new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.MinValue)),
            new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = new PageSetup { Paper = new PaperCatalog().Default } });

        var recorder = new Recorder();
        document.PaintPage(0, recorder);

        Assert.Equal(["https://example.org/object"], recorder.Links);
    }
}

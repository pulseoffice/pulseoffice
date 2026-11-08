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

public sealed class CellStylePrintRound125Tests
{
    private sealed class Recorder : IPageCanvas
    {
        public List<(string Text, DocumentRect Bounds)> Texts { get; } = [];
        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));
        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints((text?.Length ?? 0) * 5.0);
        public void FillRectangle(DocumentRect rect, PrintColor color) { }
        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) { }
        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) => Texts.Add((text, bounds));
        public void DrawPath(PrintPath path) { }
        public void DrawImage(PageImage image, DocumentRect bounds) { }
    }

    private static DocumentRect Printed(CellStyle style, CellHorizontalAlignment horizontal = CellHorizontalAlignment.Left)
    {
        var styles = new StyleCatalog();
        var sheet = new Worksheet("Sheet1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("x"));
        sheet.SetStyle(new CellAddress(0, 0), styles.Intern(style with { Horizontal = horizontal }));
        var document = new SheetPrintDocument(sheet, styles,
            new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.MinValue)),
            new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = new PageSetup { Paper = new PaperCatalog().Default } });
        var recorder = new Recorder();
        document.PaintPage(0, recorder);
        return Assert.Single(recorder.Texts, text => text.Text == "x").Bounds;
    }

    [Fact]
    public void An_indented_cell_prints_its_text_further_in_as_on_screen()
    {
        var plain = Printed(CellStyle.Default);
        var indented = Printed(CellStyle.Default with { Indent = 2 });

        Assert.True(indented.Left.Value > plain.Left.Value);
    }

    [Fact]
    public void A_right_aligned_indented_cell_keeps_its_indent_from_the_right_edge()
    {
        var plain = Printed(CellStyle.Default, CellHorizontalAlignment.Right);
        var indented = Printed(CellStyle.Default with { Indent = 2 }, CellHorizontalAlignment.Right);

        Assert.True(indented.Right.Value < plain.Right.Value);
    }
}

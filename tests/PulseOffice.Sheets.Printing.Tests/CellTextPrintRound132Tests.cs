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

public sealed class CellTextPrintRound132Tests
{
    private sealed class Recorder : IPageCanvas
    {
        public List<(string Text, DocumentRect Bounds, PrintFont Font)> Texts { get; } = [];
        public List<PrintGlyphRun> Runs { get; } = [];
        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));
        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints((text?.Length ?? 0) * 5.0);
        public void FillRectangle(DocumentRect rect, PrintColor color) { }
        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) { }
        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) => Texts.Add((text, bounds, font));
        public void DrawPath(PrintPath path) { }
        public void DrawImage(PageImage image, DocumentRect bounds) { }
        public void DrawGlyphs(PrintGlyphRun run) => Runs.Add(run);
    }

    private static Recorder Printed(string text, CellStyle style)
    {
        var styles = new StyleCatalog();
        var sheet = new Worksheet("Sheet1");
        sheet.Layout.SetColumnWidth(0, Twip.FromPoints(60));
        sheet.Layout.SetRowHeight(0, Twip.FromPoints(80));
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text(text));
        sheet.SetStyle(new CellAddress(0, 0), styles.Intern(style));
        var document = new SheetPrintDocument(sheet, styles,
            new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.MinValue)),
            new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = new PageSetup { Paper = new PaperCatalog().Default } });
        var recorder = new Recorder();
        document.PaintPage(0, recorder);
        return recorder;
    }

    [Fact]
    public void A_wrapped_cell_prints_its_text_on_several_lines_that_fit_the_column()
    {
        var recorder = Printed("alpha beta gamma delta", CellStyle.Default with { WrapText = true });

        var lines = recorder.Texts.Where(text => text.Text.Length > 0).ToList();
        Assert.True(lines.Count > 1);
        Assert.All(lines, line => Assert.True(line.Text.Length * 5.0 <= 60));
        Assert.Equal(lines.Count, lines.Select(line => line.Bounds.Top).Distinct().Count());
        Assert.Equal("alpha beta gamma delta", string.Join(" ", lines.Select(line => line.Text)));
    }

    [Fact]
    public void A_wrapped_cell_follows_its_vertical_alignment_as_a_block()
    {
        var top = Printed("alpha beta gamma delta", CellStyle.Default with { WrapText = true, Vertical = CellVerticalAlignment.Top });
        var bottom = Printed("alpha beta gamma delta", CellStyle.Default with { WrapText = true, Vertical = CellVerticalAlignment.Bottom });

        Assert.True(top.Texts[0].Bounds.Top.Value < bottom.Texts[0].Bounds.Top.Value);
    }

    [Fact]
    public void A_line_break_in_the_text_prints_two_lines()
    {
        var recorder = Printed("one\ntwo", CellStyle.Default);

        Assert.Contains(recorder.Texts, text => text.Text == "one");
        Assert.Contains(recorder.Texts, text => text.Text == "two");
        Assert.DoesNotContain(recorder.Texts, text => text.Text.Contains('\n', StringComparison.Ordinal));
    }

    [Fact]
    public void A_rotated_cell_prints_a_turned_glyph_run_instead_of_flat_text()
    {
        var recorder = Printed("up", CellStyle.Default with { RotationDegrees = 90 });

        var run = Assert.Single(recorder.Runs);
        Assert.Equal(90, run.RotationDegrees);
        Assert.Equal("up", run.Text);
        Assert.DoesNotContain(recorder.Texts, text => text.Text == "up");
    }

    [Fact]
    public void Underline_double_underline_and_strikethrough_reach_the_printed_font()
    {
        var single = Printed("u", CellStyle.Default with { Font = CellFont.Default with { Underline = true } });
        var twice = Printed("u", CellStyle.Default with { Font = CellFont.Default with { DoubleUnderline = true } });
        var struck = Printed("u", CellStyle.Default with { Font = CellFont.Default with { Strikethrough = true } });

        Assert.Equal(PrintUnderline.Single, Assert.Single(single.Texts, text => text.Text == "u").Font.Underline);
        Assert.Equal(PrintUnderline.Double, Assert.Single(twice.Texts, text => text.Text == "u").Font.Underline);
        Assert.True(Assert.Single(struck.Texts, text => text.Text == "u").Font.Strikethrough);
    }
}

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

public sealed class Round239SheetStructureTests
{
    private sealed class Recorder : IPageCanvas
    {
        private PrintTag? _tag;

        public List<(string Text, PrintTag? Tag)> Texts { get; } = [];

        public List<PrintTag?> Fills { get; } = [];

        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints((text?.Length ?? 0) * 5.0);

        public void FillRectangle(DocumentRect rect, PrintColor color) => Fills.Add(_tag);

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) => Fills.Add(_tag);

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) => Texts.Add((text, _tag));

        public void DrawPath(PrintPath path)
        {
        }

        public void DrawImage(PageImage image, DocumentRect bounds)
        {
        }

        public void Tag(PrintTag? tag) => _tag = tag;
    }

    [Fact]
    public void Cell_text_is_tagged_as_table_cells_with_title_rows_as_headers_and_grid_as_artifact()
    {
        var styles = new StyleCatalog();
        var sheet = new Worksheet("Conti");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Nome"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("Anna"));
        sheet.SetStyle(new CellAddress(1, 0), styles.Intern(CellStyle.Default with { BackgroundHex = "#FFEEAA" }));
        var document = new SheetPrintDocument(sheet, styles,
            new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.MinValue)),
            new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = new PageSetup { Paper = new PaperCatalog().Default, PrintGridLines = true }, RepeatedRows = 1 });
        var recorder = new Recorder();

        document.PaintPage(0, recorder);

        var header = Assert.Single(recorder.Texts, text => text.Text == "Nome").Tag;
        var cell = Assert.Single(recorder.Texts, text => text.Text == "Anna").Tag;
        Assert.Equal(PrintRole.TableHeader, header?.Role);
        Assert.Equal(PrintRole.TableCell, cell?.Role);
        Assert.Equal(PrintRole.TableRow, cell?.Parent?.Role);
        Assert.Equal(PrintRole.Table, cell?.Parent?.Parent?.Role);
        Assert.Equal(PrintRole.Sheet, cell?.Parent?.Parent?.Parent?.Role);
        Assert.NotEqual(header?.Parent?.Key, cell?.Parent?.Key);
        Assert.All(recorder.Fills, tag => Assert.Null(tag));
    }
}

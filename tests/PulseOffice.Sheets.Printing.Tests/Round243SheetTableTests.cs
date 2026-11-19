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

public sealed class Round243SheetTableTests
{
    private sealed class Recorder : IPageCanvas
    {
        private PrintTag? _tag;

        public List<(string Text, PrintTag? Tag)> Texts { get; } = [];

        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints((text?.Length ?? 0) * 5.0);

        public void FillRectangle(DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

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
    public void A_sheet_over_several_pages_is_one_table_with_repeats_as_artifacts_and_title_columns_as_row_headers()
    {
        var styles = new StyleCatalog();
        var sheet = new Worksheet("Conti");
        string[][] cells = [["Zona", "Totale"], ["Nord", "10"], ["Sud", "20"], ["Est", "30"]];
        for (var row = 0; row < cells.Length; row++)
            for (var column = 0; column < 2; column++)
                sheet.SetValue(new CellAddress(row, column), CellValue.Text(cells[row][column]));
        var document = new SheetPrintDocument(sheet, styles,
            new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.MinValue)),
            new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = new PageSetup { Paper = new PaperCatalog().Default }, RepeatedRows = 1, RepeatedColumns = 1, RowBreaks = new HashSet<int> { 2 } });
        var first = new Recorder();
        var second = new Recorder();

        document.PaintPage(0, first);
        document.PaintPage(1, second);

        var north = Assert.Single(first.Texts, text => text.Text == "Nord").Tag;
        var south = Assert.Single(second.Texts, text => text.Text == "Sud").Tag;
        Assert.Equal(PrintRole.TableHeader, north?.Role);
        Assert.Equal(PrintHeaderScope.Row, north?.Scope);
        Assert.Equal(PrintHeaderScope.Both, Assert.Single(first.Texts, text => text.Text == "Zona").Tag?.Scope);
        Assert.Equal(north?.Parent?.Parent?.Key, south?.Parent?.Parent?.Key);
        Assert.Null(Assert.Single(second.Texts, text => text.Text == "Totale").Tag);
    }
}

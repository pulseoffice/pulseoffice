using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Exchange;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.UI.Exchange;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round124ExchangeGridTests
{
    private readonly ExchangeCellStyles _styles = new();

    private SheetRangeCapture Capture() => new(
        new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.FromOADate(serial))),
        new EmptySpreadsheetFormats(), _styles, NullLogger<SheetRangeCapture>.Instance);

    [Fact]
    public void The_captured_grid_carries_merges_borders_fonts_alignment_and_wrap()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("head"));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Text("body"));
        sheet.Merges.Add(new CellRange(new CellAddress(0, 0), new CellAddress(0, 1)));
        sheet.Merges.Add(new CellRange(new CellAddress(5, 0), new CellAddress(6, 0)));
        var style = CellStyle.Default with
        {
            Font = CellFont.Default with { Family = "Georgia", Size = 14, Strikethrough = true },
            Horizontal = CellHorizontalAlignment.Center,
            Vertical = CellVerticalAlignment.Top,
            WrapText = true,
            Borders = CellBorders.None with { Bottom = new BorderEdge(BorderStyle.Dashed, "#FF0000", Twip.FromPoints(1.5)) },
        };
        sheet.SetStyle(new CellAddress(1, 1), workbook.Styles.Intern(style));

        var grid = Capture().Grid(workbook, sheet, new CellRange(new CellAddress(0, 0), new CellAddress(1, 1)));

        Assert.Equal([new ExchangeMerge(0, 0, 1, 2)], grid.Merges);
        var cell = grid.At(1, 1);
        Assert.Equal("Georgia", cell.FontFamily);
        Assert.Equal(14, cell.FontSizePoints);
        Assert.True(cell.Strikethrough);
        Assert.True(cell.Wrap);
        Assert.Equal(ExchangeHorizontalAlignment.Center, cell.Horizontal);
        Assert.Equal(ExchangeVerticalAlignment.Top, cell.Vertical);
        Assert.Equal(new ExchangeBorderLine(1.5, "#FF0000") { Style = ExchangeLineStyle.Dashed }, cell.Borders!.Bottom);
        Assert.Null(cell.Borders.Top);
    }

    [Fact]
    public void A_grid_from_another_process_pastes_with_its_merges_and_formatting()
    {
        var workbook = Workbook.CreateDefault();
        var grid = new CellRangeContent
        {
            SheetName = "S",
            RowCount = 2,
            ColumnCount = 2,
            Rows =
            [
                [new ExchangeCell { Kind = ExchangeCellKind.Text, Text = "head", Horizontal = ExchangeHorizontalAlignment.Center, Wrap = true }, ExchangeCell.Empty],
                [new ExchangeCell { Kind = ExchangeCellKind.Number, Number = 2, Text = "2", FontFamily = "Georgia", FontSizePoints = 9, Strikethrough = true, Vertical = ExchangeVerticalAlignment.Top, Borders = new ExchangeBorders { Left = new ExchangeBorderLine(2.25, "#00FF00") { Style = ExchangeLineStyle.Double } } }, ExchangeCell.Empty],
            ],
            Merges = [new ExchangeMerge(0, 0, 1, 2)],
        };

        var snapshot = new ExchangeGridSnapshots(new StyleTransplant(), _styles).FromGrid(grid, workbook);

        Assert.Equal([new CellRange(new CellAddress(0, 0), new CellAddress(0, 1))], snapshot.Merges);
        var head = workbook.Styles.Get(snapshot.Cells.Single(entry => entry.Row == 0 && entry.Column == 0).Cell.StyleId);
        Assert.Equal(CellHorizontalAlignment.Center, head.Horizontal);
        Assert.True(head.WrapText);
        var body = workbook.Styles.Get(snapshot.Cells.Single(entry => entry.Row == 1 && entry.Column == 0).Cell.StyleId);
        Assert.Equal("Georgia", body.Font.Family);
        Assert.Equal(9, body.Font.Size);
        Assert.True(body.Font.Strikethrough);
        Assert.Equal(CellVerticalAlignment.Top, body.Vertical);
        Assert.Equal(new BorderEdge(BorderStyle.Double, "#00FF00", Twip.FromPoints(2.25)), body.Borders.Left);
    }

    [Fact]
    public void Capture_and_paste_give_back_the_same_style()
    {
        var style = CellStyle.Default with
        {
            Font = CellFont.Default with { Family = "Georgia", Size = 12, Bold = true, Italic = true, Underline = true, Strikethrough = true, ColorHex = "#123456" },
            BackgroundHex = "#ABCDEF",
            Horizontal = CellHorizontalAlignment.Right,
            Vertical = CellVerticalAlignment.Middle,
            WrapText = true,
            Borders = new CellBorders(
                new BorderEdge(BorderStyle.Thin, "#000000", Twip.FromPoints(0.75)),
                new BorderEdge(BorderStyle.Medium, "#111111", Twip.FromPoints(1.5)),
                new BorderEdge(BorderStyle.Thick, "#222222", Twip.FromPoints(2.25)),
                new BorderEdge(BorderStyle.Dotted, "#333333", Twip.FromPoints(1))),
        };

        var described = _styles.Describe(new ExchangeCell { Kind = ExchangeCellKind.Text, Text = "x" }, style);

        Assert.Equal(style, _styles.Style(described));
    }

    [Fact]
    public void The_captured_text_of_a_general_number_keeps_the_full_precision()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1234567.891234));

        var grid = Capture().Grid(workbook, sheet, new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)));

        Assert.Equal("1234567.891234", grid.At(0, 0).Text);
    }

    private sealed class EmptySpreadsheetFormats : PulseOffice.Formats.ISpreadsheetFormatRegistry
    {
        public IReadOnlyList<PulseOffice.Formats.SpreadsheetFormat> ReadableFormats => [];

        public IReadOnlyList<PulseOffice.Formats.SpreadsheetFormat> WritableFormats => [];

        public PulseOffice.Formats.SpreadsheetFormat Native => throw new NotSupportedException();

        public PulseOffice.Formats.ISpreadsheetReader? ReaderFor(string path) => null;

        public PulseOffice.Formats.ISpreadsheetWriter? WriterFor(string path) => null;

        public PulseOffice.Formats.ISpreadsheetWriter? WriterFor(PulseOffice.Formats.SpreadsheetFormat format) => null;

        public PulseOffice.Formats.ISpreadsheetReader? ReaderForContent(Stream stream) => null;

        public PulseOffice.Foundation.Diagnostics.OperationResult<Workbook> Read(string path, Stream stream) => throw new NotSupportedException();
    }
}

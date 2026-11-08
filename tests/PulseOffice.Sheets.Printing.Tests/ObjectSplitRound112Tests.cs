using System.Globalization;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Printing.Tests;

public class ObjectSplitRound112Tests
{
    private sealed class Recorder : IPageCanvas
    {
        public List<string> Events { get; } = [];

        public List<PrintPath> Paths { get; } = [];

        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints((text?.Length ?? 0) * 5.0);

        public void FillRectangle(DocumentRect rect, PrintColor color) { }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) { }

        public void DrawText(
            string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle)
        { }

        public void DrawPath(PrintPath path)
        {
            Paths.Add(path);
            Events.Add("path");
        }

        public void DrawImage(PageImage image, DocumentRect bounds) { }

        public IDisposable Clip(DocumentRect area)
        {
            Events.Add("clip");
            return new PageClipScope(() => Events.Add("unclip"));
        }
    }

    private readonly IPaperCatalog _papers = new PaperCatalog();

    private ICellPresenter Presenter() => new CellPresenter(new NumberFormatter(
        CultureInfo.InvariantCulture,
        serial => new DateOnly(1899, 12, 30).ToDateTime(TimeOnly.MinValue).AddDays(serial)));

    private Worksheet Sheet(ObjectAnchoring anchoring = ObjectAnchoring.Move)
    {
        var sheet = new Worksheet("A");
        for (var row = 0; row < 8; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        sheet.Objects.Add(new PlacedObject
        {
            Id = "s1",
            Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Rectangle },
            Anchor = new CellAddress(2, 0),
            Size = new DocumentSize(Twip.FromPoints(40), sheet.Layout.DefaultRowHeight * 3),
            Anchoring = anchoring,
        });
        return sheet;
    }

    private SheetPrintDocument Build(Worksheet sheet) =>
        new(sheet, new Styling.StyleCatalog(), Presenter(), new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = new PageSetup { Paper = _papers.Default }, RowBreaks = new HashSet<int> { 3 } });

    [Fact]
    public void An_object_across_a_page_break_prints_its_part_on_each_page()
    {
        var document = Build(Sheet());
        var first = new Recorder();
        var second = new Recorder();

        document.PaintPage(0, first);
        document.PaintPage(1, second);

        Assert.Contains("clip", first.Events);
        Assert.Contains("path", first.Events);
        Assert.Contains("clip", second.Events);
        Assert.Contains("path", second.Events);
        Assert.True(second.Paths[0].Bounds.Top.Value < first.Paths[0].Bounds.Top.Value);
    }

    [Fact]
    public void A_move_and_size_object_over_hidden_rows_disappears_from_print()
    {
        var sheet = Sheet(ObjectAnchoring.MoveAndSize);
        sheet.Layout.HideRows(2, 4);
        var canvas = new Recorder();

        var document = Build(sheet);
        for (var page = 0; page < document.PageCount; page++) document.PaintPage(page, canvas);

        Assert.Empty(canvas.Paths);
    }
}

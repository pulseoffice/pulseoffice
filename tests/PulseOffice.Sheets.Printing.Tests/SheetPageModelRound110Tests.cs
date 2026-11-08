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

public class SheetPageModelRound110Tests
{
    private sealed class Recorder : IPageCanvas
    {
        public List<string> Texts { get; } = [];

        public List<PrintPath> Paths { get; } = [];

        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints((text?.Length ?? 0) * 5.0);

        public void FillRectangle(DocumentRect rect, PrintColor color) { }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) { }

        public void DrawText(
            string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) =>
            Texts.Add(text);

        public void DrawPath(PrintPath path) => Paths.Add(path);

        public void DrawImage(PageImage image, DocumentRect bounds) { }
    }

    private readonly IPaperCatalog _papers = new PaperCatalog();

    private ICellPresenter Presenter() => new CellPresenter(new NumberFormatter(
        CultureInfo.InvariantCulture,
        serial => new DateOnly(1899, 12, 30).ToDateTime(TimeOnly.MinValue).AddDays(serial)));

    private Worksheet Rows(string name, int rows)
    {
        var sheet = new Worksheet(name);
        for (var row = 0; row < rows; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        return sheet;
    }

    private SheetPrintDocument Build(Worksheet sheet, PageSetup setup, IJobPageNumbering? numbering = null, IReadOnlySet<int>? rowBreaks = null) =>
        new(sheet, new Styling.StyleCatalog(), Presenter(), new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = setup, RowBreaks = rowBreaks }, numbering: numbering);

    private PageSetup Footed() => new() { Paper = _papers.Default, Footer = new HeaderFooter(Center: "P&P/&N") };

    private string FooterOf(IPrintableDocument document, int page)
    {
        var canvas = new Recorder();
        document.PaintPage(page, canvas);
        return canvas.Texts.First(text => text.StartsWith('P'));
    }

    [Fact]
    public void Workbook_numbering_continues_across_sheets_and_counts_the_whole_job()
    {
        var numbering = new JobPageNumbering();
        var first = Build(Rows("A", 3), Footed(), numbering, new HashSet<int> { 1, 2 });
        var second = Build(Rows("B", 2), Footed(), numbering, new HashSet<int> { 1 });
        numbering.Number([new NumberedPart(first, true, null), new NumberedPart(second, true, null)]);
        var job = new CompositePrintDocument([first, second]);

        Assert.Equal(5, job.PageCount);
        Assert.Equal("P1/5", FooterOf(job, 0));
        Assert.Equal("P4/5", FooterOf(job, 3));
        Assert.Equal("P5/5", FooterOf(job, 4));
    }

    [Fact]
    public void First_page_number_restarts_the_count_of_its_sheet()
    {
        var numbering = new JobPageNumbering();
        var first = Build(Rows("A", 2), Footed(), numbering, new HashSet<int> { 1 });
        var second = Build(Rows("B", 2), Footed() with { FirstPageNumber = 10 }, numbering, new HashSet<int> { 1 });
        numbering.Number([new NumberedPart(first, true, null), new NumberedPart(second, true, 10)]);

        Assert.Equal("P2/4", FooterOf(first, 1));
        Assert.Equal("P10/4", FooterOf(second, 0));
        Assert.Equal("P11/4", FooterOf(second, 1));
    }

    [Fact]
    public void A_single_sheet_starts_at_its_first_page_number()
    {
        var document = Build(Rows("A", 2), Footed() with { FirstPageNumber = 7 }, rowBreaks: new HashSet<int> { 1 });

        Assert.Equal("P7/2", FooterOf(document, 0));
        Assert.Equal("P8/2", FooterOf(document, 1));
    }

    [Fact]
    public void First_and_even_pages_use_their_own_headers()
    {
        var setup = new PageSetup
        {
            Paper = _papers.Default,
            Header = new HeaderFooter(Center: "Podd"),
            FirstHeader = new HeaderFooter(Center: "Pfirst"),
            EvenHeader = new HeaderFooter(Center: "Peven"),
            DifferentFirstPage = true,
            DifferentOddEven = true,
        };
        var document = Build(Rows("A", 4), setup, rowBreaks: new HashSet<int> { 1, 2, 3 });

        Assert.Equal("Pfirst", FooterOf(document, 0));
        Assert.Equal("Peven", FooterOf(document, 1));
        Assert.Equal("Podd", FooterOf(document, 2));
        Assert.Equal("Peven", FooterOf(document, 3));
    }

    [Fact]
    public void A_break_on_a_hidden_row_applies_at_the_next_visible_row()
    {
        var sheet = Rows("A", 6);
        sheet.Layout.HideRows(3, 3);
        var document = Build(sheet, Footed(), rowBreaks: new HashSet<int> { 3 });

        Assert.Equal(2, document.PageCount);
        Assert.Equal(4, document.Pages[1].Rows.First);
    }

    [Fact]
    public void An_object_anchored_in_a_hidden_row_is_still_printed()
    {
        var sheet = Rows("A", 6);
        sheet.Objects.Add(new PlacedObject
        {
            Id = "s1",
            Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Rectangle },
            Anchor = new CellAddress(2, 0),
            Size = new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(1)),
        });
        sheet.Layout.HideRows(2, 2);
        var canvas = new Recorder();

        Build(sheet, Footed()).PaintPage(0, canvas);

        Assert.Single(canvas.Paths);
    }

    [Fact]
    public void An_object_is_printed_on_the_page_where_it_starts_only()
    {
        var sheet = Rows("A", 6);
        sheet.Objects.Add(new PlacedObject
        {
            Id = "s1",
            Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Rectangle },
            Anchor = new CellAddress(4, 0),
            Size = new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(1)),
        });
        var document = Build(sheet, Footed(), rowBreaks: new HashSet<int> { 3 });
        var first = new Recorder();
        var second = new Recorder();

        document.PaintPage(0, first);
        document.PaintPage(1, second);

        Assert.Empty(first.Paths);
        Assert.Single(second.Paths);
    }
}

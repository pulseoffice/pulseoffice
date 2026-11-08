using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

internal sealed class TableSample
{
    public const string StyleName = "Box List Blue";

    public const uint HeaderFill = 0xFF2563EB;

    public const uint HeaderInk = 0xFFFFFFFF;

    public const uint BandFill = 0xFFDBEAFE;

    public const uint Accent = 0xFF1D4ED8;

    public static readonly BorderLine Dashed = new(1.5, 0xFFC00000) { Style = BorderLineStyle.Dashed };

    public TextDocument Build()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.Table.Add(new TableStyle(StyleName)
        {
            ParentName = DefaultStyleNames.DefaultTableStyle,
            Table = new TableProperties { BorderColor = Accent, BorderWidth = 0.5, HeaderBackground = HeaderFill, HeaderColor = HeaderInk, HeaderBold = true, BandBackground = BandFill, CellPadding = 2.8 },
        });
        TableCell Cell(string id, string text, bool heading = false) =>
            new(new BlockContainer([new ParagraphBlock(id, new TextParagraph(text) { StyleName = heading ? DefaultStyleNames.TableHeading : DefaultStyleNames.TableContents }.WithRuns([new TextRun(text)]))]));
        TableCell Covered() => new(new BlockContainer()) { Covered = true };
        var merged = Cell("m", "Unite");
        merged.ColumnSpan = 2;
        var tall = Cell("tall", "Alta");
        tall.RowSpan = 2;
        tall.VerticalAlign = CellVerticalAlign.Middle;
        var dashed = Cell("dash", "Bordo");
        dashed.Borders = new BoxBorders { Bottom = Dashed, Top = new BorderLine(0.5, Accent), Left = new BorderLine(0.5, Accent), Right = new BorderLine(0.5, Accent) };
        var tinted = Cell("tint", "Tinta");
        tinted.Background = 0xFFFFF3C4;
        var sum = Cell("sum", "5");
        sum.Formula = "SUM(B3:B4)";
        var rows = new List<TableRow>
        {
            new([Cell("h0", "Nome", true), Cell("h1", "Valore", true), Cell("h2", "Nota", true)]),
            new([merged, Covered(), tall]),
            new([dashed, Cell("v1", "2"), Covered()]) { AllowBreakAcrossPages = false },
            new([tinted, Cell("v2", "3"), Cell("n3", "fine")]) { Height = Twip.FromPoints(30), MinimumHeight = false },
            new([Cell("t", "Totale"), sum, Cell("n4", "")]),
        };
        var table = new TableBlock("tab", rows, [new Twip(2000), new Twip(3000), new Twip(4000)])
        {
            StyleName = StyleName,
            HeaderRows = 1,
            Name = "Prezzi",
        };
        document.Body.Replace([new ParagraphBlock("before", "Prima della tabella", DefaultStyleNames.TextBody), table, new ParagraphBlock("after", "Dopo", DefaultStyleNames.TextBody)]);
        return document;
    }
}

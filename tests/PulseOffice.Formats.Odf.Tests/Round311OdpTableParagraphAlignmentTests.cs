using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round311OdpTableParagraphAlignmentTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Fact]
    public void Paragraph_alignment_inside_a_cell_survives_the_round_trip()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        var rich = new TableCellText(0, 0, [new TableCellParagraph([new TableTextRun("plain")]), new TableCellParagraph([new TableTextRun("bold")]) { Alignment = CellHorizontalAlign.Right }, new TableCellParagraph([new TableTextRun("even")]) { Alignment = CellHorizontalAlign.Justify }]);
        var header = new TableCellText(TableCellFormat.HeaderRow, 0, [new TableCellParagraph([new TableTextRun("A")]) { Alignment = CellHorizontalAlign.Center }]);
        slide.Add(new ObjectElement
        {
            Id = "t1",
            Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(6000), new Twip(3000)),
            Content = new DataTableObject
            {
                Id = "t1",
                Query = new DataQuerySource { ConnectionId = "", Sql = "" },
                Snapshot = new DataSnapshot { Columns = [new DataColumn("A", DataValueKind.Text)], Rows = [["plain\nbold\neven"]] },
                CellTexts = [rich, header],
            },
        });

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(buffer).Value!;
        var texts = back.Slides[0].Elements.OfType<ObjectElement>().Select(o => o.Content).OfType<DataTableObject>().Single().CellTexts;

        Assert.Contains(rich, texts);
        Assert.Contains(header, texts);
    }
}

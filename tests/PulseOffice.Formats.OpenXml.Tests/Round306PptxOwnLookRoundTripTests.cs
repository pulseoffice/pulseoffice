using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round306PptxOwnLookRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Fact]
    public void An_own_look_table_reads_back_without_invented_rich_text()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        var look = TableCellLook.Plain with { Bold = true, TextHex = "#DC2626", FontSize = 14 };
        slide.Add(new ObjectElement
        {
            Id = "t1",
            Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(6000), new Twip(3000)),
            Content = new DataTableObject
            {
                Id = "t1",
                Query = new DataQuerySource { ConnectionId = "", Sql = "" },
                Snapshot = new DataSnapshot { Columns = [new DataColumn("H", DataValueKind.Text)], Rows = [["x"]] },
                Style = new DataTableStyle { OwnLook = true, Grid = false },
                CellFormats =
                [
                    new TableCellFormat(TableCellFormat.HeaderRow, 0, CellVerticalAlign.Top) { Look = look },
                    new TableCellFormat(0, 0, CellVerticalAlign.Top) { Look = look },
                ],
            },
        });

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(buffer).Value!.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<DataTableObject>().Single();

        Assert.True(back.Style.OwnLook);
        Assert.Empty(back.CellTexts);
        Assert.Equal("H", back.Snapshot.Columns[0].Name);
        Assert.Equal("x", back.Snapshot.Rows[0][0]);
    }
}

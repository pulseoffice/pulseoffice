using PulseOffice.Document.Embedding;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round173TableStyleFlagsTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck(DataTableStyle style)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement
        {
            Id = "t",
            Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(4000), new Twip(1000)),
            Content = new DataTableObject
            {
                Id = "t",
                Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
                Snapshot = new DataSnapshot
                {
                    Columns = [new DataColumn("a", DataValueKind.Text), new DataColumn("b", DataValueKind.Text)],
                    Rows = [["1", "2"], ["3", "4"]],
                },
                Style = style,
            },
        });
        deck.AddExisting(slide);
        return deck;
    }

    private Presentation ThroughPptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return new PptxReader(_themes, _masters).Read(buffer).Value!;
    }

    private Presentation ThroughOdp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return new OdpReader(_themes, _masters).Read(buffer).Value!;
    }

    private DataTableStyle StyleOf(Presentation deck) =>
        ((DataTableObject)deck.Slides[0].Elements.OfType<ObjectElement>().Single().Content).Style;

    [Fact]
    public void First_and_last_column_last_row_and_banded_columns_cross_both_formats()
    {
        var deck = Deck(new DataTableStyle { HeaderRow = true, FirstColumn = true, LastColumn = true, LastRow = true, BandedColumns = true });

        foreach (var again in new[] { ThroughPptx(deck), ThroughOdp(deck), ThroughPptx(ThroughOdp(deck)), ThroughOdp(ThroughPptx(deck)) })
        {
            var style = StyleOf(again);
            Assert.True(style.FirstColumn);
            Assert.True(style.LastColumn);
            Assert.True(style.LastRow);
            Assert.True(style.BandedColumns);
        }
    }

    [Fact]
    public void Flags_left_off_stay_off()
    {
        var style = StyleOf(ThroughOdp(ThroughPptx(Deck(new DataTableStyle()))));

        Assert.False(style.FirstColumn || style.LastColumn || style.LastRow || style.BandedColumns);
    }
}

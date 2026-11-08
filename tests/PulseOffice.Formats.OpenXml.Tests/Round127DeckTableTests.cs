using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round127DeckTableTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private readonly TableCellLook _look = new()
    {
        FillHex = "#336699",
        Top = new TableBorderLine("#000000", 1),
        TextHex = "#FFFFFF",
        Bold = true,
        Italic = true,
        Underline = true,
        Strikethrough = true,
        FontFamily = "Georgia",
        FontSize = 18,
    };

    private Presentation Sample(IReadOnlyList<TableCellFormat> formats, bool ownLook = true)
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement
        {
            Id = "t1",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.FromCentimeters(2), PulseOffice.Foundation.Units.Twip.FromCentimeters(2),
                PulseOffice.Foundation.Units.Twip.FromCentimeters(12), PulseOffice.Foundation.Units.Twip.FromCentimeters(4)),
            Content = new DataTableObject
            {
                Id = "t1",
                Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
                Snapshot = new DataSnapshot { Columns = [new("A", DataValueKind.Text), new("B", DataValueKind.Text)], Rows = [["1", "2"]] },
                Style = new DataTableStyle { HeaderRow = true, OwnLook = ownLook, Grid = !ownLook },
                CellFormats = formats,
            },
        });
        deck.AddExisting(slide);
        return deck;
    }

    private DataTableObject Table(Presentation deck) =>
        deck.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<DataTableObject>().Single();

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

    [Fact]
    public void Italic_underline_strike_and_font_family_of_a_cell_cross_pptx_and_odp()
    {
        var sample = Sample([new TableCellFormat(0, 1, CellVerticalAlign.Top) { Look = _look }]);
        foreach (var back in new[] { ThroughPptx(sample), ThroughOdp(sample), ThroughPptx(ThroughOdp(sample)), ThroughOdp(ThroughPptx(sample)) })
            Assert.Equal(_look, Table(back).CellFormats.Single(format => format.Row == 0 && format.Column == 1).Look);
    }

    [Fact]
    public void The_vertical_alignment_of_a_header_cell_survives_odp()
    {
        var sample = Sample([new TableCellFormat(TableCellFormat.HeaderRow, 0, CellVerticalAlign.Center) { Look = _look }]);

        var header = Table(ThroughOdp(sample)).CellFormats.Single(format => format.Row == TableCellFormat.HeaderRow && format.Column == 0);

        Assert.Equal(CellVerticalAlign.Center, header.VerticalAlign);
    }

    [Fact]
    public void Run_formatting_of_a_table_without_style_or_cell_properties_is_read()
    {
        using var stream = new MemoryStream();
        using (var buffer = new MemoryStream())
        {
            Assert.True(new PptxWriter().Write(Sample([], ownLook: false), buffer).Succeeded);
            stream.Write(buffer.ToArray());
        }
        using (var document = PresentationDocument.Open(stream, true))
        {
            var table = document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<D.Table>()).Single();
            Assert.DoesNotContain(table.Descendants<D.TableCellProperties>(), properties => properties.HasChildren);
            var run = table.Descendants<D.Run>().Last();
            run.RunProperties = new D.RunProperties { Italic = true, Bold = true };
        }
        stream.Position = 0;

        var read = Table(new PptxReader(_themes, _masters).Read(stream).Value!);

        var look = read.CellFormats.Single(format => format.Row == 0 && format.Column == 1).Look;
        Assert.True(look?.Italic);
        Assert.True(look?.Bold);
    }
}

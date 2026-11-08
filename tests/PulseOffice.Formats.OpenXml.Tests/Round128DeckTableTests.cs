using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using System.IO.Compression;
using System.Xml.Linq;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round128DeckTableTests
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
    public void A_table_with_an_entirely_plain_own_look_keeps_its_own_look_through_odp()
    {
        var sample = Sample([new TableCellFormat(0, 0, CellVerticalAlign.Top) { Look = TableCellLook.Plain }]);

        foreach (var back in new[] { ThroughOdp(sample), ThroughPptx(sample), ThroughOdp(ThroughPptx(sample)) })
        {
            var table = Table(back);
            Assert.True(table.Style.OwnLook);
            Assert.False(table.Style.Grid);
        }
    }

    [Fact]
    public void A_generic_table_with_a_centred_cell_stays_generic_through_odp()
    {
        var table = Table(ThroughOdp(Sample([new TableCellFormat(0, 0, CellVerticalAlign.Center)], ownLook: false)));

        Assert.False(table.Style.OwnLook);
        Assert.Equal(CellVerticalAlign.Center, table.CellFormats.Single(format => format.Row == 0 && format.Column == 0).VerticalAlign);
    }

    private MemoryStream Pptx(Action<D.Table> change)
    {
        var stream = new MemoryStream();
        using (var buffer = new MemoryStream())
        {
            Assert.True(new PptxWriter().Write(Sample([new TableCellFormat(0, 1, CellVerticalAlign.Top) { Look = _look }]), buffer).Succeeded);
            stream.Write(buffer.ToArray());
        }
        using (var document = PresentationDocument.Open(stream, true))
            change(document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<D.Table>()).Single());
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void A_pptx_table_cell_with_differently_formatted_runs_declares_the_loss()
    {
        using var stream = Pptx(table =>
        {
            var paragraph = table.Descendants<D.Run>().Last().Parent!;
            paragraph.Append(new D.Run(new D.RunProperties { Bold = false, Italic = false }, new D.Text("plain")));
        });

        var result = new PptxReader(_themes, _masters).Read(stream);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PPTX225");
    }

    [Fact]
    public void A_pptx_table_whose_cells_have_one_format_each_declares_nothing()
    {
        using var stream = Pptx(table =>
        {
            var run = table.Descendants<D.Run>().Last();
            run.Parent!.Append(new D.Run((D.RunProperties)run.RunProperties!.CloneNode(true), new D.Text("more")));
        });

        var result = new PptxReader(_themes, _masters).Read(stream);

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "PPTX225");
    }

    private MemoryStream Odp(Func<XElement, bool> change)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(Sample([]), buffer).Succeeded);
        var stream = new MemoryStream();
        stream.Write(buffer.ToArray());
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.GetEntry("content.xml")!;
            XDocument content;
            using (var read = entry.Open()) content = XDocument.Load(read);
            Assert.True(change(content.Root!));
            entry.Delete();
            using var write = zip.CreateEntry("content.xml").Open();
            content.Save(write);
        }
        stream.Position = 0;
        return stream;
    }

    private static readonly XNamespace TableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    [Fact]
    public void An_odp_table_cell_with_formatted_spans_declares_the_loss()
    {
        using var stream = Odp(root =>
        {
            var paragraph = root.Descendants(TableNs + "table-cell").SelectMany(cell => cell.Elements(TextNs + "p")).Last();
            paragraph.Add(new XElement(TextNs + "span", new XAttribute(TextNs + "style-name", "T1"), "bold"));
            return true;
        });

        var result = new OdpReader(_themes, _masters).Read(stream);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ODF281");
    }

    [Fact]
    public void An_odp_table_written_by_deck_declares_nothing_about_spans()
    {
        var result = new OdpReader(_themes, _masters).Read(Odp(_ => true));

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "ODF281");
    }
}

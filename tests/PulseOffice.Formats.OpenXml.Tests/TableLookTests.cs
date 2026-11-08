using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class TableLookTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private readonly TableCellLook _look = new()
    {
        FillHex = "#336699",
        Top = new TableBorderLine("#000000", 3),
        Bottom = new TableBorderLine("#FF0000", 1),
        TextHex = "#FFFFFF",
        Bold = true,
        FontSize = 20,
        Horizontal = CellHorizontalAlign.Center,
    };

    private Presentation Sample(bool ownLook)
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
                CellFormats = ownLook ? [new TableCellFormat(TableCellFormat.HeaderRow, 0, CellVerticalAlign.Top) { Look = _look }, new TableCellFormat(0, 1, CellVerticalAlign.Center) { Look = _look }] : [],
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
    public void L_aspetto_delle_celle_attraversa_PPTX_e_ODP()
    {
        foreach (var back in new[] { ThroughPptx(Sample(true)), ThroughOdp(Sample(true)), ThroughPptx(ThroughOdp(Sample(true))) })
        {
            var table = Table(back);
            Assert.True(table.Style.OwnLook);
            Assert.Equal(_look, table.CellFormats.Single(format => format.Row == TableCellFormat.HeaderRow && format.Column == 0).Look);
            var body = table.CellFormats.Single(format => format.Row == 0 && format.Column == 1);
            Assert.Equal(_look, body.Look);
            Assert.Equal(CellVerticalAlign.Center, body.VerticalAlign);
        }
    }

    [Fact]
    public void Le_tabelle_senza_aspetto_proprio_restano_tali()
    {
        Assert.False(Table(ThroughPptx(Sample(false))).Style.OwnLook);
        Assert.False(Table(ThroughOdp(Sample(false))).Style.OwnLook);
    }

    [Fact]
    public void Lo_stile_predefinito_di_PowerPoint_si_risolve_dal_suo_identificativo()
    {
        using var stream = new MemoryStream();
        using (var buffer = new MemoryStream())
        {
            Assert.True(new PptxWriter().Write(Sample(false), buffer).Succeeded);
            stream.Write(buffer.ToArray());
        }
        using (var document = PresentationDocument.Open(stream, true))
        {
            var table = document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<D.Table>()).Single();
            table.TableProperties!.Append(new D.TableStyleId("{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}"));
            document.PresentationPart.TableStylesPart?.TableStyleList?.RemoveAllChildren();
        }
        stream.Position = 0;

        var read = Table(new PptxReader(_themes, _masters).Read(stream).Value!);

        Assert.True(read.Style.OwnLook);
        var header = read.CellFormats.Single(format => format.Row == TableCellFormat.HeaderRow && format.Column == 0).Look!;
        var body = read.CellFormats.Single(format => format.Row == 0 && format.Column == 0).Look!;
        Assert.True(header.Bold);
        Assert.NotNull(header.FillHex);
        Assert.NotEqual(header.FillHex, body.FillHex);
        Assert.Equal(3, header.Bottom!.WidthPoints, 1);
    }

    [Fact]
    public void Gli_stili_a_tema_usano_sfondo_della_tabella_linee_del_tema_e_trasparenza()
    {
        var path = "/Volumes/Dati/develop/libreoffice-ref/sd/qa/unit/data/pptx/bnc480256.pptx";
        Assert.SkipWhen(!File.Exists(path), "Reference corpus not available.");
        using var stream = File.OpenRead(path);
        var tables = new PptxReader(_themes, _masters).Read(stream).Value!.Slides[0].Elements.OfType<ObjectElement>()
            .Select(element => element.Content).OfType<DataTableObject>().ToList();

        Assert.Equal(2, tables.Count);
        Assert.All(tables, table => Assert.True(table.Style.OwnLook));
        var first = tables[0].CellFormats.Single(format => format.Row == 0 && format.Column == 0).Look!;
        var second = tables[0].CellFormats.Single(format => format.Row == 1 && format.Column == 0).Look!;
        Assert.NotNull(first.Top);
        Assert.NotEqual(first.FillHex, second.FillHex);
        Assert.Contains(tables[1].CellFormats, format => format.Look?.FillHex is not null);
    }
}

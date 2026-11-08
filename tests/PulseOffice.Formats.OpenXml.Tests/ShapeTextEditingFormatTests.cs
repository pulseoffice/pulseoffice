using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ShapeTextEditingFormatTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly IShapeTextEditing _editing = new ShapeTextEditing();

    private ShapeObject Written()
    {
        var shape = _editing.WithText(new ShapeObject { Id = "forma", Shape = ShapeKind.Ellipse }, "Ciao mondo\nseconda riga");
        shape = _editing.Format(shape, 5, 10, run => run with { Bold = true, ColorHex = "#C00000" });
        return _editing.Format(shape, 11, 18, run => run with { Italic = true, Size = 20 });
    }

    private void Same(ShapeObject expected, ShapeObject actual)
    {
        Assert.Equal(_editing.PlainText(expected), _editing.PlainText(actual).Replace("\r\n", "\n"));
        var text = _editing.PlainText(actual);
        var bold = text.IndexOf("mondo", StringComparison.Ordinal);
        Assert.True(_editing.RunAt(actual, bold, bold + 5).Bold);
        Assert.Equal("#C00000", _editing.RunAt(actual, bold, bold + 5).ColorHex?.ToUpperInvariant());
        var italic = text.IndexOf("seconda", StringComparison.Ordinal);
        Assert.True(_editing.RunAt(actual, italic, italic + 7).Italic);
        Assert.Equal(20, _editing.RunAt(actual, italic, italic + 7).Size);
        Assert.False(_editing.RunAt(actual, 0, 4).Bold);
    }

    private Presentation Deck(ShapeObject shape)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement { Id = "forma", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(2000)), Content = shape });
        deck.AddExisting(slide);
        return deck;
    }

    private ShapeObject Single(Presentation deck) =>
        deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single();

    [Fact]
    public void Il_testo_scritto_e_formattato_in_una_forma_di_Deck_attraversa_PPTX_e_ODP()
    {
        var shape = Written();
        var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(shape), pptx).Succeeded);
        pptx.Position = 0;
        var fromPptx = Single(new PptxReader(_themes, _masters).Read(pptx).Value!);
        Same(shape, fromPptx);

        var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(shape), odp).Succeeded);
        odp.Position = 0;
        Same(shape, Single(new OdpReader(_themes, _masters).Read(odp).Value!));
    }

    [Fact]
    public void Il_testo_centrato_delle_forme_ODP_porta_lo_stile_di_paragrafo_che_le_altre_suite_leggono()
    {
        var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(Written()), odp).Succeeded);
        odp.Position = 0;
        using var zip = new System.IO.Compression.ZipArchive(odp);
        var content = System.Xml.Linq.XDocument.Load(zip.GetEntry("content.xml")!.Open());
        System.Xml.Linq.XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        System.Xml.Linq.XNamespace style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        System.Xml.Linq.XNamespace fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
        var paragraphs = content.Descendants(text + "p").Where(p => p.Ancestors().Any(a => a.Name.LocalName == "custom-shape")).ToList();
        Assert.Equal(2, paragraphs.Count);
        var name = Assert.Single(paragraphs.Select(p => (string?)p.Attribute(text + "style-name")).Distinct());
        var declared = content.Descendants(style + "style").Single(s => (string?)s.Attribute(style + "name") == name);
        Assert.Equal("center", (string?)declared.Element(style + "paragraph-properties")!.Attribute(fo + "text-align"));
    }

    [Fact]
    public void Il_testo_scritto_e_formattato_in_una_forma_di_Ledger_attraversa_XLSX_e_ODS()
    {
        var shape = Written();
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "forma",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(3)),
            Content = shape,
        });

        var xlsx = new MemoryStream();
        new XlsxWriter().Write(workbook, xlsx);
        var fromXlsx = new XlsxReader().Read(new MemoryStream(xlsx.ToArray())).Value!;
        Same(shape, Assert.IsType<ShapeObject>(Assert.Single(fromXlsx.Sheets[0].Objects).Content));

        var ods = new MemoryStream();
        new OdsWriter().Write(workbook, ods);
        var fromOds = new OdsReader().Read(new MemoryStream(ods.ToArray())).Value!;
        Same(shape, Assert.IsType<ShapeObject>(Assert.Single(fromOds.Sheets[0].Objects).Content));
    }
}

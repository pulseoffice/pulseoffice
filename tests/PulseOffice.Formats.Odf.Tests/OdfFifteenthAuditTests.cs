using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfFifteenthAuditTests
{
    private readonly XNamespace _dom = "http://www.w3.org/2001/xml-events";
    private readonly XNamespace _script = "urn:oasis:names:tc:opendocument:xmlns:script:1.0";
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private XElement Content(MemoryStream stream)
    {
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        using var entry = archive.GetEntry("content.xml")!.Open();
        return XDocument.Load(entry).Root!;
    }

    private Presentation SoundDeck()
    {
        var deck = new Presentation { Theme = _themes.Default, Master = _masters.CreateStandard(_themes.Default) };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        deck.Slides[0].Add(new TextElement
        {
            Id = "box",
            Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(10), Twip.FromCentimeters(4)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("Suona")]),
        });
        deck.Sounds.Add(new EmbeddedSound("Media/campana.wav", "audio/wav", [.. "RIFF$\0\0\0WAVEfmt "u8.ToArray(), 1, 2, 3, 4]));
        deck.Slides[0].SetInteraction(new InteractiveAction { TargetId = "box", Kind = InteractionKind.PlaySound, SoundPath = "Media/campana.wav" });
        return deck;
    }

    [Fact]
    public void Il_nome_dom_click_del_suono_al_clic_si_risolve_nello_spazio_degli_eventi()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(SoundDeck(), stream).Succeeded);

        var listener = Content(stream).Descendants(OdfPackage.Namespaces.Presentation + "event-listener").Single();

        Assert.Equal(_dom, listener.GetNamespaceOfPrefix("dom"));
        Assert.Equal("dom:click", (string?)listener.Attribute(_script + "event-name"));
        Assert.Equal("script", listener.GetPrefixOfNamespace(_script));
    }

    [Fact]
    public void Foglio_e_testo_dichiarano_lo_spazio_degli_eventi_dom()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Foglio1");
        using var sheet = new MemoryStream();
        new OdsWriter().Write(workbook, sheet);
        using var text = new MemoryStream();
        Assert.True(new OdtWriter().Write(new TextDocument(new DefaultStyleSheetFactory().Create()), text).Succeeded);

        Assert.Equal(_dom, Content(sheet).GetNamespaceOfPrefix("dom"));
        Assert.Equal(_dom, Content(text).GetNamespaceOfPrefix("dom"));
        Assert.Equal(_script, Content(text).GetNamespaceOfPrefix("script"));
    }

    private MemoryStream Ods(string rows)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write("application/vnd.oasis.opendocument.spreadsheet");
            using var content = new StreamWriter(archive.CreateEntry("content.xml").Open());
            content.Write("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" "
                + "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" "
                + "xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.3\"><office:body><office:spreadsheet><table:table table:name=\"Foglio1\">"
                + rows + "</table:table></office:spreadsheet></office:body></office:document-content>");
        }

        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Una_cornice_di_testo_collegata_in_table_shapes_si_importa_con_il_collegamento_e_torna_in_ODS()
    {
        using var source = Ods("<table:shapes><draw:a xlink:type=\"simple\" xlink:href=\"http://www.google.com/\"><draw:frame draw:z-index=\"0\" svg:width=\"60.85mm\" svg:height=\"20.45mm\" svg:x=\"25.06mm\" svg:y=\"13.28mm\">"
            + "<draw:text-box><text:p>Text box</text:p></draw:text-box></draw:frame></draw:a></table:shapes><table:table-row><table:table-cell/></table:table-row>");
        var workbook = new OdsReader().Read(source).Value!;

        var placed = Assert.Single(workbook.Sheets[0].Objects);
        var shape = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeObject>(placed.Content);
        Assert.Equal("Text box", shape.Text);
        Assert.True(shape.Style.Text.AutomaticColor);
        Assert.Equal("#000000", new PulseOffice.Drawing.Shapes.ShapeTextInk().Resolve(shape.Style, PulseOffice.Drawing.Shapes.ShapeTextInk.PaperHex));
        Assert.Equal("http://www.google.com/", placed.Hyperlink);

        using var written = new MemoryStream();
        new OdsWriter().Write(workbook, written);
        written.Position = 0;
        var again = Assert.Single(new OdsReader().Read(written).Value!.Sheets[0].Objects);
        Assert.Equal("http://www.google.com/", again.Hyperlink);
        Assert.Equal("Text box", Assert.IsType<PulseOffice.Drawing.Shapes.ShapeObject>(again.Content).Text);
    }
}

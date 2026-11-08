using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class ComputedGeometryOdfTests
{
    private readonly XNamespace _draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private readonly PresetGeometry _snip = new("snip1Rect") { Adjustments = [new GeometryAdjustment("adj", 30000)], ReferenceWidth = 5292, ReferenceHeight = 3528 };

    private readonly FormulaGeometry _formula = new("M ?f0 0 L 21600 0 21600 21600 0 21600 Z N")
    {
        Type = "mso-spt100",
        Modifiers = [5400],
        Equations = [new EnhancedEquation("f0", "$0*2")],
        Handles = [new EnhancedHandle([new("handle-position", "$0 top")])],
    };

    private Presentation Deck(ComputedGeometry geometry)
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement
        {
            Id = "forma",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(3000), new Twip(2000)),
            Content = new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle, Computed = geometry },
        });
        deck.AddExisting(slide);
        return deck;
    }

    private (Presentation Deck, XDocument Content) RoundTrip(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        XDocument content;
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true))
        using (var entry = archive.GetEntry("content.xml")!.Open())
            content = XDocument.Load(entry);
        buffer.Position = 0;
        return (new OdpReader(_themes, _masters).Read(buffer).Value!, content);
    }

    private ShapeObject Shape(Presentation deck) =>
        deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single();

    [Fact]
    public void Una_forma_predefinita_si_scrive_con_tipo_modificatori_ed_equazioni_e_si_rilegge()
    {
        var (back, content) = RoundTrip(Deck(_snip));

        var geometry = content.Descendants(_draw + "enhanced-geometry").Single();
        Assert.Equal("ooxml-snip1Rect", (string?)geometry.Attribute(_draw + "type"));
        Assert.Equal("30000", (string?)geometry.Attribute(_draw + "modifiers"));
        Assert.NotEmpty(geometry.Elements(_draw + "equation"));
        var read = Assert.IsType<PresetGeometry>(Shape(back).Computed);
        Assert.Equal("snip1Rect", read.Name);
        Assert.Equal(new GeometryAdjustment("adj", 30000), read.Adjustments.Single());
    }

    [Fact]
    public void Una_geometria_a_formule_attraversa_odp_intatta()
    {
        var (back, _) = RoundTrip(Deck(_formula));

        var read = Assert.IsType<FormulaGeometry>(Shape(back).Computed);
        Assert.Equal(_formula.Path, read.Path);
        Assert.Equal(_formula.Equations, read.Equations);
        Assert.Equal(_formula.Modifiers, read.Modifiers);
        Assert.Equal(_formula.Handles, read.Handles);
        Assert.Equal("mso-spt100", read.Type);
    }

    [Fact]
    public void Una_forma_predefinita_attraversa_ods()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "forma",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
            Content = new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle, Computed = _snip },
        });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var back = new OdsReader().Read(stream).Value!;

        var shape = Assert.IsType<ShapeObject>(Assert.Single(back.Sheets[0].Objects).Content);
        Assert.Equal(_snip, shape.Computed);
    }

    [Fact]
    public void Le_equazioni_di_riferimento_in_odt_si_valutano_senza_dichiarazione()
    {
        var formulaShape = "<draw:custom-shape text:anchor-type=\"paragraph\" svg:width=\"2cm\" svg:height=\"2cm\"><draw:enhanced-geometry svg:viewBox=\"0 0 21600 21600\" draw:type=\"mso-spt100\" draw:modifiers=\"5400\" "
            + "draw:enhanced-path=\"M ?f0 0 L 21600 0 21600 21600 0 21600 Z N\"><draw:equation draw:name=\"f0\" draw:formula=\"$0 *2\"/></draw:enhanced-geometry></draw:custom-shape>";
        var read = new OdtReader().Read(Odt(formulaShape));

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODT181");
        Assert.Contains(read.Value!.Objects.All.Select(item => item.Content).OfType<PulseOffice.Documents.Objects.EmbeddedContent>(),
            content => content.Value is ShapeObject { Computed: FormulaGeometry });
    }

    private MemoryStream Odt(string shape)
    {
        var content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" "
            + "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.3\">"
            + $"<office:body><office:text><text:p>a{shape}</text:p></office:text></office:body></office:document-content>";
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("mimetype").Open())) writer.Write("application/vnd.oasis.opendocument.text");
            using (var writer = new StreamWriter(archive.CreateEntry("content.xml").Open())) writer.Write(content);
        }
        stream.Position = 0;
        return stream;
    }
}

using System.IO.Compression;
using System.Text;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ArcShapeTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private const string Namespaces =
        "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
        "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.3\"";

    private Presentation ReadOdp(Stream stream) => new OdpReader(_themes, _masters).Read(stream).Value!;

    private Presentation ForeignArcs()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string text)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(text));
            }
            Add("mimetype", "application/vnd.oasis.opendocument.presentation");
            Add("content.xml", "<?xml version=\"1.0\"?><office:document-content " + Namespaces + "><office:body><office:presentation><draw:page draw:name=\"p\">" +
                "<draw:ellipse svg:width=\"8cm\" svg:height=\"4cm\" svg:x=\"1cm\" svg:y=\"1cm\" draw:kind=\"arc\" draw:start-angle=\"270\" draw:end-angle=\"45\"/>" +
                "<draw:ellipse svg:width=\"8cm\" svg:height=\"4cm\" svg:x=\"1cm\" svg:y=\"6cm\" draw:kind=\"section\" draw:start-angle=\"30\" draw:end-angle=\"120\"/>" +
                "<draw:ellipse svg:width=\"8cm\" svg:height=\"4cm\" svg:x=\"1cm\" svg:y=\"11cm\" draw:kind=\"cut\" draw:start-angle=\"180\" draw:end-angle=\"270\"/>" +
                "</draw:page></office:presentation></office:body></office:document-content>");
        }
        buffer.Position = 0;
        return ReadOdp(buffer);
    }

    private IReadOnlyList<ShapeObject> Shapes(Presentation deck) =>
        [.. deck.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().OrderBy(shape => shape.Shape)];

    private void AssertArcs(Presentation deck)
    {
        var shapes = Shapes(deck);
        Assert.Equal([ShapeKind.Arc, ShapeKind.Pie, ShapeKind.Chord], shapes.Select(shape => shape.Shape));
        Assert.Equal((270, 45), (shapes[0].ArcStartDegrees, shapes[0].ArcEndDegrees));
        Assert.Equal((30, 120), (shapes[1].ArcStartDegrees, shapes[1].ArcEndDegrees));
        Assert.Equal((180, 270), (shapes[2].ArcStartDegrees, shapes[2].ArcEndDegrees));
        Assert.Equal(ShapeFillKind.None, shapes[0].Style.Fill.Kind);
    }

    [Fact]
    public void Archi_settori_e_segmenti_di_riferimento_si_leggono_e_attraversano_ODP_e_PPTX()
    {
        var deck = ForeignArcs();
        AssertArcs(deck);

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, odp).Succeeded);
        odp.Position = 0;
        AssertArcs(ReadOdp(odp));

        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, pptx).Succeeded);
        pptx.Position = 0;
        AssertArcs(new PptxReader(_themes, _masters).Read(pptx).Value!);
    }

    [Fact]
    public void Il_settore_si_disegna_dal_centro_lungo_l_arco_in_senso_antiorario()
    {
        var outline = new ShapeGeometry().Outline(new ShapeObject { Id = "p", Shape = ShapeKind.Pie, ArcStartDegrees = 0, ArcEndDegrees = 90 });
        var points = outline.Figures[0].Segments.OfType<LineSegment>().Select(segment => segment.To).ToList();
        Assert.Equal(1, points[0].X, 3);
        Assert.Equal(0.5, points[0].Y, 3);
        Assert.Equal(0.5, points[^1].X, 3);
        Assert.Equal(0, points[^1].Y, 3);
    }
}

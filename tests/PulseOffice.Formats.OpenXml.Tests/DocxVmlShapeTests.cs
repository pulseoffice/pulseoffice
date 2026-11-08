using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxVmlShapeTests
{
    private const string Namespaces = "xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private MemoryStream WithPicture(string vml)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock(document.NextId("p"), "x", DefaultStyleNames.Standard));
        var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            var picture = new W.Picture($"<w:pict {Namespaces}>{vml}</w:pict>");
            package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().First().Append(new W.Run(picture));
        }
        stream.Position = 0;
        return stream;
    }

    private (TextDocument Document, IReadOnlyList<string> Codes) Read(string vml)
    {
        using var stream = WithPicture(vml);
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return (read.Value!, [.. read.Diagnostics.Select(diagnostic => diagnostic.Code)]);
    }

    private TextDocument ThroughDocx(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
    }

    private TextDocument ThroughOdt(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return new OdtReader().Read(stream).Value!;
    }

    private T Single<T>(TextDocument document) =>
        Assert.IsType<T>(Assert.IsType<EmbeddedContent>(Assert.Single(document.Objects.All).Content).Value);

    [Fact]
    public void Un_ovale_VML_senza_casella_di_testo_si_importa_come_ellisse()
    {
        var (document, codes) = Read("<v:oval style=\"position:absolute;margin-left:10pt;margin-top:20pt;width:100pt;height:50pt\" fillcolor=\"red\" strokecolor=\"#00f\"/>");

        Assert.DoesNotContain("DOCX102", codes);
        var shape = Single<ShapeObject>(document);
        Assert.Equal(ShapeKind.Ellipse, shape.Shape);
        Assert.Equal("#FF0000", shape.Style.Fill.ColorHex);
        Assert.Equal("#0000FF", shape.Style.Line.ColorHex);
        var item = Assert.Single(document.Objects.All);
        Assert.Equal(Twip.FromPoints(100), item.Size.Width);
        Assert.Equal(Twip.FromPoints(10), item.Placement!.X);
    }

    [Fact]
    public void Una_linea_VML_prende_misura_e_posizione_dai_suoi_estremi()
    {
        var (document, codes) = Read("<v:line style=\"position:absolute\" from=\"10pt,40pt\" to=\"110pt,20pt\" strokeweight=\"2pt\"/>");

        Assert.DoesNotContain("DOCX102", codes);
        var line = Single<ShapeObject>(document);
        Assert.Equal(ShapeKind.Line, line.Shape);
        Assert.True(line.FlipVertical);
        Assert.Equal(Twip.FromPoints(2), line.Style.Line.Width);
        var item = Assert.Single(document.Objects.All);
        Assert.Equal(Twip.FromPoints(100), item.Size.Width);
        Assert.Equal(Twip.FromPoints(20), item.Size.Height);
        Assert.Equal(Twip.FromPoints(10), item.Placement!.X);
        Assert.Equal(Twip.FromPoints(20), item.Placement!.Y);
    }

    [Fact]
    public void Una_forma_VML_con_percorso_diventa_figura_libera_e_attraversa_DOCX_e_ODT()
    {
        var (document, codes) = Read("<v:shape style=\"width:100pt;height:100pt\" coordsize=\"1000,1000\" path=\"m,l1000,,1000,1000xe\" fillcolor=\"yellow\"/>");

        Assert.DoesNotContain("DOCX102", codes);
        Assert.DoesNotContain("DOCX140", codes);
        var freeform = Single<FreeformObject>(document);
        var figure = Assert.Single(freeform.Path.Figures);
        Assert.True(figure.Closed);
        Assert.Equal([new ShapePoint(0, 0), new ShapePoint(1, 0), new ShapePoint(1, 1)], figure.Nodes.Select(node => node.Point));
        Assert.Equal("#FFFF00", freeform.Style.Fill.ColorHex);

        foreach (var again in new[] { ThroughDocx(document), ThroughOdt(document) })
        {
            var back = Single<FreeformObject>(again);
            var outline = Assert.Single(back.Path.Figures);
            Assert.True(outline.Closed);
            Assert.Contains(outline.Nodes, node => Math.Abs(node.Point.X - 1) < 0.001 && Math.Abs(node.Point.Y - 1) < 0.001);
            Assert.Contains(outline.Nodes, node => Math.Abs(node.Point.X - 1) < 0.001 && Math.Abs(node.Point.Y) < 0.001);
            Assert.Equal("#FFFF00", back.Style.Fill.ColorHex, ignoreCase: true);
        }
    }

    [Fact]
    public void Una_spezzata_VML_diventa_figura_libera_aperta()
    {
        var (document, codes) = Read("<v:polyline style=\"position:absolute\" points=\"0,0,50pt,25pt,100pt,0\" filled=\"f\"/>");

        Assert.DoesNotContain("DOCX102", codes);
        var freeform = Single<FreeformObject>(document);
        var figure = Assert.Single(freeform.Path.Figures);
        Assert.False(figure.Closed);
        Assert.Equal(new ShapePoint(0.5, 1), figure.Nodes[1].Point);
        Assert.Equal(Twip.FromPoints(100), Assert.Single(document.Objects.All).Size.Width);
    }

    [Fact]
    public void Il_WordArt_VML_diventa_fontwork()
    {
        var (document, codes) = Read(
            "<v:shapetype id=\"_x0000_t144\" coordsize=\"21600,21600\" o:spt=\"144\" path=\"al10800,10800,10800,10800@3@15e\"/>"
            + "<v:shape type=\"#_x0000_t144\" style=\"width:200pt;height:60pt\" fillcolor=\"#c00000\">"
            + "<v:textpath style=\"font-family:&quot;Arial Black&quot;;font-weight:bold\" string=\"Ciao\"/></v:shape>");

        Assert.DoesNotContain("DOCX102", codes);
        var fontwork = Single<FontworkObject>(document);
        Assert.Equal("Ciao", fontwork.Text);
        Assert.Equal(FontworkWarp.ArchUp, fontwork.Warp);
        Assert.Equal("Arial Black", fontwork.FontFamily);
        Assert.True(fontwork.Bold);
        Assert.Equal("#C00000", fontwork.Style.Fill.ColorHex);
        Assert.Equal("Ciao", Single<FontworkObject>(ThroughOdt(document)).Text);
    }

    [Fact]
    public void Un_preset_VML_noto_si_importa_con_la_sua_forma()
    {
        var (document, codes) = Read(
            "<v:shapetype id=\"_x0000_t74\" coordsize=\"21600,21600\" o:spt=\"74\" path=\"m10860,2187c@0,0e\"/>"
            + "<v:shape type=\"#_x0000_t74\" style=\"width:50pt;height:50pt\"/>");

        Assert.DoesNotContain("DOCX140", codes);
        Assert.Equal(ShapeKind.Heart, Single<ShapeObject>(document).Shape);
    }

    [Fact]
    public void Un_percorso_calcolato_da_formule_resta_rettangolo_dichiarato()
    {
        var (document, codes) = Read("<v:shape style=\"width:50pt;height:50pt\" coordsize=\"21600,21600\" path=\"m@0,l@1,21600xe\"/>");

        Assert.Contains("DOCX140", codes);
        Assert.DoesNotContain("DOCX102", codes);
        Assert.Equal(ShapeKind.Rectangle, Single<ShapeObject>(document).Shape);
    }

    [Fact]
    public void Un_disegno_che_definisce_solo_un_tipo_di_forma_non_dichiara_perdite()
    {
        var (document, codes) = Read("<v:shapetype id=\"_x0000_t136\" fillcolor=\"red\"/>");

        Assert.DoesNotContain("DOCX102", codes);
        Assert.Empty(document.Objects.All);
    }
}

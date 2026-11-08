using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxThirteenthAuditTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Il_testo_dentro_gli_smart_tag_si_legge()
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx(
            "<w:p><w:r><w:t xml:space=\"preserve\">Via </w:t></w:r><w:smartTag w:uri=\"urn:schemas-microsoft-com:office:smarttags\" w:element=\"address\">"
            + "<w:smartTag w:uri=\"urn:schemas-microsoft-com:office:smarttags\" w:element=\"Street\"><w:smartTagPr><w:attr w:name=\"x\" w:val=\"y\"/></w:smartTagPr>"
            + "<w:r><w:rPr><w:b/></w:rPr><w:t>Roma</w:t></w:r></w:smartTag><w:r><w:t xml:space=\"preserve\"> 2</w:t></w:r></w:smartTag></w:p>"));

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(read.Value!.Body.Blocks));
        Assert.Equal("Via Roma 2", paragraph.Text);
    }

    [Fact]
    public void Un_gruppo_dentro_un_gruppo_si_scrive_e_si_rilegge()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", "Corpo") { Marks = [new ObjectMark(0, "g1")] }]);
        var inner = new ShapeGroupObject
        {
            Id = "interno",
            Children = [new GroupChild(new ShapeObject { Id = "b", Shape = ShapeKind.Ellipse }, 0, 0, 0.5, 1), new GroupChild(new ShapeObject { Id = "c", Shape = ShapeKind.Triangle }, 0.5, 0, 0.5, 1)],
        };
        var outer = new ShapeGroupObject { Id = "esterno", Children = [new GroupChild(new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle }, 0, 0, 0.5, 1), new GroupChild(inner, 0.5, 0, 0.5, 1)] };
        document.Objects.Add(new AnchoredObject("g1", new CharacterAnchor("p1"), new EmbeddedContent(outer), new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(3))));

        var writer = _composer.Writer(DocxFlavour.Document);
        Assert.DoesNotContain(writer.CheckFidelity(document).Losses, loss => loss.Code == "DOCX002");
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;

        var group = Assert.IsType<ShapeGroupObject>(Assert.IsType<EmbeddedContent>(Assert.Single(read.Objects.All).Content).Value);
        var nested = Assert.Single(group.Children.Select(child => child.Content).OfType<ShapeGroupObject>());
        Assert.Equal([ShapeKind.Ellipse, ShapeKind.Triangle], nested.Children.Select(child => ((ShapeObject)child.Content).Shape));
        var placed = group.Children.Single(child => child.Content is ShapeGroupObject);
        Assert.Equal(0.5, placed.X, 2);
        Assert.Equal(0.5, placed.Width, 2);
    }

    [Fact]
    public void A_chart_frame_without_a_chart_inside_a_group_is_declared()
    {
        const string Spaces = "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" "
            + "xmlns:wpg=\"http://schemas.microsoft.com/office/word/2010/wordprocessingGroup\" xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"";
        var shape = "<wps:wsp><wps:cNvSpPr/><wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"914400\" cy=\"914400\"/></a:xfrm><a:prstGeom prst=\"ellipse\"><a:avLst/></a:prstGeom></wps:spPr><wps:bodyPr/></wps:wsp>";
        var chart = "<wpg:graphicFrame><wpg:cNvPr id=\"3\" name=\"Grafico\"/><wpg:cNvFrPr/><wpg:xfrm><a:off x=\"914400\" y=\"0\"/><a:ext cx=\"914400\" cy=\"914400\"/></wpg:xfrm>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"/></a:graphic></wpg:graphicFrame>";
        var group = $"<w:p><w:r><w:drawing><wp:inline {Spaces}><wp:extent cx=\"1828800\" cy=\"914400\"/><wp:docPr id=\"1\" name=\"Gruppo\"/><a:graphic>"
            + "<a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingGroup\"><wpg:wgp><wpg:cNvGrpSpPr/><wpg:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"1828800\" cy=\"914400\"/>"
            + $"<a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"1828800\" cy=\"914400\"/></a:xfrm></wpg:grpSpPr>{shape}{chart}</wpg:wgp></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>";

        var read = _composer.Reader(DocxFlavour.Document).Read(Docx(group));
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX102");
    }

    [Fact]
    public void Un_campo_dentro_il_risultato_di_un_campo_di_modulo_si_dichiara()
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx(
            "<w:p><w:r><w:fldChar w:fldCharType=\"begin\"><w:ffData><w:name w:val=\"Testo1\"/><w:enabled/><w:textInput/></w:ffData></w:fldChar></w:r>"
            + "<w:r><w:instrText xml:space=\"preserve\"> FORMTEXT </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>"
            + "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> DATE </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>"
            + "<w:r><w:t>19/10/2026</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>"));

        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX161");
    }

    [Fact]
    public void Uno_stile_di_pagina_inutilizzato_con_il_solo_numero_di_pagina_si_dichiara()
    {
        var document = new TextDocument();
        var unused = new PageStyle("Preliminari");
        unused.Footer.Enabled = true;
        unused.Footer.Default.Add(new ParagraphBlock("pf", new PulseOffice.Text.Rich.TextParagraph(string.Empty)
            .WithRuns([new PulseOffice.Text.Rich.TextRun(string.Empty) { Field = PulseOffice.Text.Rich.TextFieldKind.PageNumber }])));
        document.Styles.Page.Add(unused);

        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX171");

        var tabled = new TextDocument();
        var withTable = new PageStyle("Convertito");
        withTable.Footer.Enabled = true;
        withTable.Footer.Default.Add(new TableBlock("t1", [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("c1", string.Empty)]))])], [Twip.FromCentimeters(4)]));
        tabled.Styles.Page.Add(withTable);
        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(tabled).Losses, loss => loss.Code == "DOCX171");
    }

    [Fact]
    public void L_intestazione_resta_quando_il_documento_comincia_con_una_sezione()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var standard = document.Styles.Page.Find(document.InitialPageStyle)!;
        standard.Header.Enabled = true;
        standard.Header.Default.Replace([new ParagraphBlock("h", "Intestazione", DefaultStyleNames.Header)]);
        document.Body.Add(new SectionBlock("s", "Sezione", new BlockContainer([new ParagraphBlock("p1", "dentro", DefaultStyleNames.Standard)])));
        document.Body.Add(new ParagraphBlock("p2", "fuori", DefaultStyleNames.Standard));

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;

        var page = read.Styles.Page.Find(read.InitialPageStyle)!;
        Assert.True(page.Header.Enabled);
        Assert.Equal("Intestazione", ((ParagraphBlock)page.Header.Default[0]).Text);
    }

    [Fact]
    public void L_immagine_dentro_un_gruppo_VML_resta_un_immagine()
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var picture = main.AddImagePart(ImagePartType.Png);
            using (var data = new MemoryStream(png)) picture.FeedData(data);
            var id = main.GetIdOfPart(picture);
            main.Document = new W.Document($"<w:document {Namespace} xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><w:body><w:p><w:r><w:pict>"
                + "<v:group style=\"position:absolute;width:200pt;height:100pt\" coordorigin=\"0,0\" coordsize=\"2000,1000\">"
                + "<v:rect style=\"position:absolute;left:0;top:0;width:1000;height:1000\" fillcolor=\"yellow\"/>"
                + $"<v:shape style=\"position:absolute;left:1000;top:0;width:1000;height:1000\"><v:imagedata r:id=\"{id}\"/></v:shape>"
                + "</v:group></w:pict></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var group = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeGroupObject>(Assert.IsType<PulseOffice.Documents.Objects.EmbeddedContent>(Assert.Single(read.Objects.All).Content).Value);
        Assert.Contains(group.Children, child => child.Content is PulseOffice.Drawing.Assets.RasterImageObject);
    }

    [Fact]
    public void Un_campo_calcolato_dentro_un_campo_di_modulo_si_dichiara()
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx(
            "<w:p><w:r><w:fldChar w:fldCharType=\"begin\"><w:ffData><w:name w:val=\"Testo6\"/><w:enabled w:val=\"0\"/><w:textInput><w:type w:val=\"calculated\"/><w:default w:val=\"=15+5\"/></w:textInput></w:ffData></w:fldChar></w:r>"
            + "<w:r><w:instrText xml:space=\"preserve\"> FORMTEXT </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> =15+5 </w:instrText></w:r>"
            + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:instrText>20</w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r>"
            + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>20</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>"));

        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX160");
    }

    [Fact]
    public void Un_richiamo_di_nota_senza_nota_si_dichiara()
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx("<w:p><w:r><w:t>testo</w:t></w:r><w:r><w:footnoteReference w:id=\"2\"/></w:r></w:p>"));

        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX182");
    }

    [Fact]
    public void Una_geometria_predefinita_dello_standard_si_disegna()
    {
        var read = Shape("snip1Rect");
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX183");
        Assert.Contains(read.Value!.Objects.All.Select(item => item.Content).OfType<EmbeddedContent>(), content =>
            content.Value is ShapeObject { Computed: PulseOffice.Drawing.Shapes.Geometry.PresetGeometry { Name: "snip1Rect" } });
    }

    private PulseOffice.Foundation.Diagnostics.OperationResult<TextDocument> Shape(string preset)
    {
        const string Spaces = "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" "
            + "xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"";
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx(
            $"<w:p><w:r><w:drawing><wp:inline {Spaces}><wp:extent cx=\"914400\" cy=\"914400\"/><wp:docPr id=\"1\" name=\"Forma\"/><a:graphic>"
            + "<a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"><wps:wsp><wps:cNvSpPr/><wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"914400\" cy=\"914400\"/></a:xfrm>"
            + $"<a:prstGeom prst=\"{preset}\"><a:avLst/></a:prstGeom></wps:spPr><wps:bodyPr/></wps:wsp></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>"));
        return read;
    }

    private static MemoryStream Docx(string body)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
        stream.Position = 0;
        return stream;
    }
}

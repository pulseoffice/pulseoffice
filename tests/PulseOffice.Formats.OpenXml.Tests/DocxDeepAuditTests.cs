using System.Text;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

[Collection(nameof(OpenXmlLargeDocumentPerformanceTests))]
public sealed class DocxDeepAuditTests
{
    private const string Namespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private const string DrawingNamespaces = "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"";

    private static string Shape(string extent, string position) =>
        $"<w:p><w:r><w:drawing><wp:anchor {DrawingNamespaces} distT=\"-9223372036854775808\" distB=\"9223372036854775807\" distL=\"0\" distR=\"0\" simplePos=\"0\" relativeHeight=\"1024\" behindDoc=\"0\" locked=\"0\" layoutInCell=\"1\" allowOverlap=\"1\"><wp:simplePos x=\"0\" y=\"0\"/>"
        + $"<wp:positionH relativeFrom=\"page\"><wp:posOffset>{position}</wp:posOffset></wp:positionH><wp:positionV relativeFrom=\"page\"><wp:posOffset>{position}</wp:posOffset></wp:positionV>"
        + $"<wp:extent {extent}/><wp:wrapNone/><wp:docPr id=\"1\" name=\"forma\"/><a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"><wps:wsp><wps:cNvSpPr/><wps:spPr><a:prstGeom prst=\"ellipse\"/></wps:spPr><wps:bodyPr/></wps:wsp></a:graphicData></a:graphic></wp:anchor></w:drawing></w:r></w:p>";

    private static MemoryStream Package(string body)
    {
        var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            using var xml = new MemoryStream(Encoding.UTF8.GetBytes($"<w:document xmlns:w=\"{Namespace}\"><w:body>{body}</w:body></w:document>"));
            main.FeedData(xml);
        }
        source.Position = 0;
        return source;
    }

    private static TextDocumentReadResultProbe Read(Stream source)
    {
        var result = new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory()).Reader(DocxFlavour.Document).Read(source);
        return new TextDocumentReadResultProbe(result.Value, result.Diagnostics.Select(loss => loss.Code).ToList());
    }

    private sealed record TextDocumentReadResultProbe(PulseOffice.Documents.TextDocument? Document, List<string> Codes);

    private static T OnSmallStack<T>(Func<T> work)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception exception) { failure = exception; }
        }, 1024 * 1024);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
        return result;
    }

    [Fact]
    public void Tabelle_annidate_senza_fine_non_fanno_traboccare_la_pila()
    {
        const int depth = 2000;
        var body = new StringBuilder();
        for (var level = 0; level < depth; level++) body.Append("<w:tbl><w:tr><w:tc>");
        body.Append("<w:p><w:r><w:t>fondo</w:t></w:r></w:p>");
        for (var level = 0; level < depth; level++) body.Append("<w:p/></w:tc></w:tr></w:tbl>");
        body.Append("<w:p><w:r><w:t>dopo</w:t></w:r></w:p>");
        using var source = Package(body.ToString());

        var read = OnSmallStack(() => Read(source));

        Assert.Contains(read.Document!.Body.Blocks.OfType<ParagraphBlock>(), paragraph => paragraph.Text == "dopo");
        Assert.Contains("DOCX220", read.Codes);
    }

    [Fact]
    public void Controlli_in_linea_annidati_senza_fine_non_fanno_traboccare_la_pila()
    {
        const int depth = 4000;
        var body = new StringBuilder("<w:p>");
        for (var level = 0; level < depth; level++) body.Append("<w:sdt><w:sdtContent>");
        body.Append("<w:r><w:t>fondo</w:t></w:r>");
        for (var level = 0; level < depth; level++) body.Append("</w:sdtContent></w:sdt>");
        body.Append("<w:r><w:t>dopo</w:t></w:r></w:p>");
        using var source = Package(body.ToString());

        var read = OnSmallStack(() => Read(source));

        Assert.Contains("dopo", read.Document!.Body.Blocks.OfType<ParagraphBlock>().Single().Text);
        Assert.Contains("DOCX220", read.Codes);
    }

    [Fact]
    public void Rientri_non_numerici_non_diventano_NaN()
    {
        using var source = Package("<w:p><w:pPr><w:ind w:left=\"NaN\" w:right=\"Infinity\" w:firstLine=\"-Infinity\"/><w:spacing w:before=\"NaN\"/></w:pPr><w:r><w:t>testo</w:t></w:r></w:p>");

        var paragraph = Read(source).Document!.Body.Blocks.OfType<ParagraphBlock>().Single().Content;

        Assert.True(paragraph.MarginLeft is null || double.IsFinite(paragraph.MarginLeft.Value));
        Assert.True(paragraph.MarginRight is null || double.IsFinite(paragraph.MarginRight.Value));
        Assert.True(paragraph.Indent is null || double.IsFinite(paragraph.Indent.Value));
        Assert.True(double.IsFinite(paragraph.SpaceBefore));
    }

    [Fact]
    public void Pagina_e_colonne_fuori_misura_restano_entro_i_limiti()
    {
        using var source = Package("<w:p><w:r><w:t>testo</w:t></w:r></w:p><w:sectPr><w:pgSz w:w=\"4294967295\" w:h=\"0\"/><w:pgMar w:top=\"-2147483648\" w:bottom=\"2147483647\" w:left=\"4294967295\" w:right=\"0\" w:header=\"4294967295\" w:footer=\"0\"/><w:cols w:num=\"32000\" w:space=\"-500\"/></w:sectPr>");

        var document = Read(source).Document!;
        var page = document.Styles.Page.Find(document.InitialPageStyle)!;

        Assert.InRange(page.Width.Value, 144, 158_400);
        Assert.InRange(page.Height.Value, 144, 158_400);
        Assert.InRange(page.Margins.Left.Value, 0, 158_400);
        Assert.InRange(page.Margins.Top.Value, 0, 158_400);
        Assert.InRange(page.Margins.Bottom.Value, 0, 158_400);
        Assert.InRange(page.Columns.Count, 1, 45);
        Assert.True(page.Columns.Gap.Value >= 0);
    }

    [Fact]
    public void Inizio_elenco_enorme_resta_nei_limiti_di_Word()
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            using (var xml = new MemoryStream(Encoding.UTF8.GetBytes($"<w:document xmlns:w=\"{Namespace}\"><w:body><w:p><w:pPr><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr></w:pPr><w:r><w:t>voce</w:t></w:r></w:p></w:body></w:document>")))
                main.FeedData(xml);
            var numbering = main.AddNewPart<NumberingDefinitionsPart>();
            using var definitions = new MemoryStream(Encoding.UTF8.GetBytes($"<w:numbering xmlns:w=\"{Namespace}\"><w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:start w:val=\"2147483647\"/><w:numFmt w:val=\"lowerLetter\"/><w:lvlText w:val=\"%1.\"/></w:lvl><w:lvl w:ilvl=\"1\"><w:start w:val=\"-5\"/></w:lvl></w:abstractNum><w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/><w:lvlOverride w:ilvl=\"2\"><w:startOverride w:val=\"999999999\"/></w:lvlOverride></w:num></w:numbering>"));
            numbering.FeedData(definitions);
        }
        source.Position = 0;

        var document = Read(source).Document!;
        var list = document.Styles.List.Find("WWNum1")!;

        Assert.All(list.Levels.Take(3), level => Assert.InRange(level.StartAt, 0, 32767));
    }

    [Fact]
    public void Altezza_di_riga_enorme_non_diventa_negativa()
    {
        using var source = Package("<w:tbl><w:tblGrid><w:gridCol w:w=\"-3000\"/></w:tblGrid><w:tr><w:trPr><w:trHeight w:val=\"4294967295\"/></w:trPr><w:tc><w:tcPr><w:gridSpan w:val=\"-4\"/></w:tcPr><w:p/></w:tc></w:tr></w:tbl><w:p/>");

        var table = Read(source).Document!.Body.Blocks.OfType<TableBlock>().Single();

        Assert.True(table.Rows[0].Height!.Value.Value > 0);
        Assert.All(table.Columns, column => Assert.True(column.Value >= 0));
    }

    [Fact]
    public void Corpo_scala_e_spaziatura_dei_caratteri_restano_nei_limiti_di_Word()
    {
        using var source = Package("<w:p><w:r><w:rPr><w:sz w:val=\"2000000000\"/><w:w w:val=\"30000\"/><w:spacing w:val=\"-2000000\"/></w:rPr><w:t>testo</w:t></w:r></w:p>");

        var run = Read(source).Document!.Body.Blocks.OfType<ParagraphBlock>().Single().Content.Runs.Single();

        Assert.InRange(run.FontSize!.Value, 1, 1638);
        Assert.InRange(run.ScaleWidth!.Value, 1, 600);
        Assert.InRange(run.LetterSpacing!.Value, -1584, 1584);
    }

    [Theory]
    [InlineData("http://exa mple.com/pagina")]
    [InlineData("a:b c")]
    [InlineData("http://[sbagliato")]
    public void Un_collegamento_con_un_indirizzo_non_valido_non_impedisce_il_salvataggio(string target)
    {
        var document = new PulseOffice.Documents.TextDocument();
        document.Body.Add(new ParagraphBlock("p1", new PulseOffice.Text.Rich.TextParagraph(string.Empty).WithRuns([new PulseOffice.Text.Rich.TextRun("vai") { Hyperlink = target }])));

        using var written = new MemoryStream();
        var result = new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory()).Writer(DocxFlavour.Document).Write(document, written);

        Assert.True(result.Succeeded);
        written.Position = 0;
        var run = Read(written).Document!.Body.Blocks.OfType<ParagraphBlock>().Single().Content.Runs.Single();
        Assert.False(string.IsNullOrEmpty(run.Hyperlink));
    }

    [Fact]
    public void Misure_di_una_forma_fuori_scala_non_traboccano_negli_interi()
    {
        using var source = Package(Shape("cx=\"9223372036854775807\" cy=\"-9223372036854775808\"", "9223372036854775807"));

        var item = Read(source).Document!.Objects.All.Single();

        Assert.InRange(item.Size.Width.Value, 0, 158_400 * 4);
        Assert.InRange(item.Size.Height.Value, 0, 158_400 * 4);
        Assert.InRange(item.Placement.X.Value, -158_400 * 4, 158_400 * 4);
        Assert.InRange(item.WrapSpacing.Top.Value, 0, 158_400 * 4);
        Assert.InRange(item.WrapSpacing.Bottom.Value, 0, 158_400 * 4);
    }

    [Theory]
    [InlineData("width:NaNpt;height:1e300pt")]
    [InlineData("width:-50pt;height:Infinitypt")]
    public void Oggetto_incorporato_con_misure_non_finite_resta_nei_limiti(string style)
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var ole = main.AddNewPart<EmbeddedObjectPart>("application/vnd.openxmlformats-officedocument.oleObject", "rIdOle");
            using (var data = new MemoryStream([1, 2, 3])) ole.FeedData(data);
            using var xml = new MemoryStream(Encoding.UTF8.GetBytes($"<w:document xmlns:w=\"{Namespace}\" xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><w:body><w:p><w:r><w:object><v:shape id=\"s\" style=\"{style}\"/><o:OLEObject Type=\"Embed\" ProgID=\"Package\" ShapeID=\"s\" r:id=\"rIdOle\"/></w:object></w:r></w:p></w:body></w:document>"));
            main.FeedData(xml);
        }
        source.Position = 0;

        var item = Read(source).Document!.Objects.All.Single();

        Assert.InRange(item.Size.Width.Value, 0, 633_600);
        Assert.InRange(item.Size.Height.Value, 0, 633_600);
    }

    [Fact]
    public void Risposte_che_si_rispondono_a_vicenda_non_formano_un_ciclo()
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            using (var xml = new MemoryStream(Encoding.UTF8.GetBytes($"<w:document xmlns:w=\"{Namespace}\"><w:body><w:p><w:commentRangeStart w:id=\"1\"/><w:r><w:t>testo</w:t></w:r><w:commentRangeEnd w:id=\"1\"/><w:r><w:commentReference w:id=\"1\"/></w:r></w:p></w:body></w:document>")))
                main.FeedData(xml);
            var comments = main.AddNewPart<WordprocessingCommentsPart>();
            using (var xml = new MemoryStream(Encoding.UTF8.GetBytes($"<w:comments xmlns:w=\"{Namespace}\" xmlns:w14=\"http://schemas.microsoft.com/office/word/2010/wordml\"><w:comment w:id=\"1\" w:author=\"a\"><w:p w14:paraId=\"0000000A\"><w:r><w:t>uno</w:t></w:r></w:p></w:comment><w:comment w:id=\"2\" w:author=\"b\"><w:p w14:paraId=\"0000000B\"><w:r><w:t>due</w:t></w:r></w:p></w:comment><w:comment w:id=\"3\" w:author=\"c\"><w:p w14:paraId=\"0000000C\"><w:r><w:t>tre</w:t></w:r></w:p></w:comment></w:comments>")))
                comments.FeedData(xml);
            var extended = main.AddNewPart<WordprocessingCommentsExPart>();
            using (var xml = new MemoryStream(Encoding.UTF8.GetBytes("<w15:commentsEx xmlns:w15=\"http://schemas.microsoft.com/office/word/2012/wordml\"><w15:commentEx w15:paraId=\"0000000A\" w15:done=\"0\"/><w15:commentEx w15:paraId=\"0000000B\" w15:paraIdParent=\"0000000C\" w15:done=\"0\"/><w15:commentEx w15:paraId=\"0000000C\" w15:paraIdParent=\"0000000B\" w15:done=\"0\"/></w15:commentsEx>")))
                extended.FeedData(xml);
        }
        source.Position = 0;

        var document = Read(source).Document!;

        foreach (var comment in document.Comments.All)
        {
            var seen = new HashSet<string>();
            for (var current = comment; current.ParentId is { } parent; current = document.Comments.Find(parent)!)
                Assert.True(seen.Add(current.Id), "ciclo di risposte");
        }
    }

    [Fact]
    public void Gruppo_VML_con_coordinate_non_finite_resta_finito()
    {
        using var source = Package("<w:p><w:r><w:pict xmlns:v=\"urn:schemas-microsoft-com:vml\"><v:group style=\"position:absolute;width:100pt;height:100pt\" coordorigin=\"NaN,0\" coordsize=\"Infinity,1e400\"><v:rect style=\"left:NaN;top:0;width:Infinity;height:10;rotation:NaN\"/><v:oval style=\"left:0;top:0;width:10;height:10\"/></v:group></w:pict></w:r></w:p>");

        var group = Read(source).Document!.Objects.All.Select(item => item.Content).OfType<PulseOffice.Documents.Objects.EmbeddedContent>()
            .Select(content => content.Value).OfType<PulseOffice.Drawing.Shapes.ShapeGroupObject>().Single();

        Assert.All(group.Children, child =>
        {
            Assert.True(double.IsFinite(child.X) && double.IsFinite(child.Y) && double.IsFinite(child.Width) && double.IsFinite(child.Height));
            Assert.True(double.IsFinite(child.RotationDegrees));
        });
    }

    [Fact]
    public void Casella_di_controllo_con_simbolo_surrogato_non_impedisce_la_lettura()
    {
        using var source = Package("<w:p><w:sdt><w:sdtPr xmlns:w14=\"http://schemas.microsoft.com/office/word/2010/wordml\"><w14:checkbox><w14:checked w14:val=\"1\"/><w14:checkedState w14:font=\"MS Gothic\" w14:val=\"D800\"/><w14:uncheckedState w14:font=\"MS Gothic\" w14:val=\"DFFF\"/></w14:checkbox></w:sdtPr><w:sdtContent><w:r><w:t>x</w:t></w:r></w:sdtContent></w:sdt><w:r><w:t> dopo</w:t></w:r></w:p>");

        var document = Read(source).Document;

        Assert.NotNull(document);
        Assert.Contains("dopo", document!.Body.Blocks.OfType<ParagraphBlock>().Single().Text);
    }

    [Fact]
    public void Un_carattere_incorporato_illeggibile_si_dichiara()
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            using (var xml = new MemoryStream(Encoding.UTF8.GetBytes($"<w:document xmlns:w=\"{Namespace}\"><w:body><w:p><w:r><w:t>testo</w:t></w:r></w:p></w:body></w:document>")))
                main.FeedData(xml);
            var table = main.AddNewPart<FontTablePart>();
            var font = table.AddFontPart(FontPartType.FontOdttf);
            using (var garbage = new MemoryStream(Enumerable.Range(0, 200).Select(index => (byte)index).ToArray())) font.FeedData(garbage);
            using var fonts = new MemoryStream(Encoding.UTF8.GetBytes($"<w:fonts xmlns:w=\"{Namespace}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><w:font w:name=\"Rotto\"><w:embedRegular r:id=\"{table.GetIdOfPart(font)}\" w:fontKey=\"{{01234567-89AB-CDEF-0123-456789ABCDEF}}\"/></w:font></w:fonts>"));
            table.FeedData(fonts);
        }
        source.Position = 0;

        var read = Read(source);

        Assert.Empty(read.Document!.Fonts.Files);
        Assert.Contains("DOCX221", read.Codes);
    }

    [Fact]
    public void Simbolo_fuori_dal_piano_di_base_non_si_tronca()
    {
        using var source = Package("<w:p><w:r><w:sym w:char=\"1F600\"/></w:r></w:p>");

        var text = Read(source).Document!.Body.Blocks.OfType<ParagraphBlock>().Single().Text;

        Assert.Equal(char.ConvertFromUtf32(0x1F600), text);
    }

    [Fact]
    public void Filigrana_con_rotazione_e_opacita_non_finite_resta_nei_limiti()
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var header = main.AddNewPart<HeaderPart>("rIdHeader");
            using (var xml = new MemoryStream(Encoding.UTF8.GetBytes($"<w:hdr xmlns:w=\"{Namespace}\" xmlns:v=\"urn:schemas-microsoft-com:vml\"><w:p><w:r><w:pict><v:shape id=\"PowerPlusWaterMarkObject1\" style=\"position:absolute;rotation:NaN\" fillcolor=\"silver\"><v:fill opacity=\"7e9\"/><v:textpath style=\"font-family:&quot;Arial&quot;\" string=\"BOZZA\"/></v:shape></w:pict></w:r></w:p></w:hdr>")))
                header.FeedData(xml);
            using var body = new MemoryStream(Encoding.UTF8.GetBytes($"<w:document xmlns:w=\"{Namespace}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><w:body><w:p/><w:sectPr><w:headerReference w:type=\"default\" r:id=\"rIdHeader\"/></w:sectPr></w:body></w:document>"));
            main.FeedData(body);
        }
        source.Position = 0;

        var document = Read(source).Document!;
        var watermark = document.Styles.Page.Find(document.InitialPageStyle)!.Watermark!;

        Assert.True(double.IsFinite(watermark.Angle));
        Assert.InRange(watermark.Transparency, 0, 100);
    }

    [Fact]
    public void Bordi_e_capolettera_fuori_misura_restano_nei_limiti_di_Word()
    {
        using var source = Package("<w:p><w:pPr><w:framePr w:dropCap=\"drop\" w:lines=\"2000000000\" w:hSpace=\"2000000000\"/></w:pPr><w:r><w:t>C</w:t></w:r></w:p>"
            + "<w:p><w:pPr><w:pBdr><w:top w:val=\"single\" w:sz=\"4294967295\" w:space=\"4294967295\"/></w:pBdr></w:pPr><w:r><w:t>iao</w:t></w:r></w:p>");

        var paragraph = Read(source).Document!.Body.Blocks.OfType<ParagraphBlock>().Single().Content;

        Assert.InRange(paragraph.DropCap!.Lines, 2, 10);
        Assert.InRange(paragraph.DropCap.Distance, 0, 1584);
        Assert.InRange(paragraph.Borders!.Top!.Width, 0, 36);
        Assert.InRange(paragraph.Borders.PaddingTop, 0, 31);
    }

    [Fact]
    public void Molti_smart_tag_si_sciolgono_in_tempo_lineare()
    {
        const int count = 20_000;
        var body = new StringBuilder($"<w:body xmlns:w=\"{Namespace}\"><w:p>");
        for (var index = 0; index < count; index++) body.Append("<w:smartTag w:uri=\"u\" w:element=\"e\"><w:smartTag w:uri=\"u\" w:element=\"i\"><w:r><w:t>x</w:t></w:r></w:smartTag></w:smartTag>");
        body.Append("</w:p></w:body>");
        var root = new DocumentFormat.OpenXml.Wordprocessing.Body(body.ToString());

        var clock = System.Diagnostics.Stopwatch.StartNew();
        new DocxSmartTags().Unwrap(root);
        clock.Stop();

        Assert.Equal(count, root.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Count());
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(8), $"{clock.Elapsed}");
    }
}

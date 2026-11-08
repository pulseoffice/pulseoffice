using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxRangeAndNoteReadingTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void La_fine_di_un_commento_fra_due_paragrafi_chiude_il_commento_nel_paragrafo_precedente()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:commentRangeStart w:id=\"0\"/></w:p><w:p><w:r><w:t>testo</w:t></w:r></w:p>"
                + "<w:commentRangeEnd w:id=\"0\"/><w:p><w:r><w:commentReference w:id=\"0\"/></w:r><w:bookmarkStart w:id=\"1\" w:name=\"segno\"/></w:p>"
                + "<w:bookmarkEnd w:id=\"1\"/></w:body></w:document>");
            main.AddNewPart<WordprocessingCommentsPart>().Comments = new W.Comments($"<w:comments {Namespace}><w:comment w:id=\"0\" w:author=\"a\"><w:p><w:r><w:t>nota</w:t></w:r></w:p></w:comment></w:comments>");
        }

        stream.Position = 0;
        var document = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var comment = Assert.Single(document.Comments.All);
        var paragraphs = document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Contains(paragraphs[0].Marks, mark => mark is CommentMark { Edge: MarkEdge.Start } start && start.CommentId == comment.Id);
        Assert.Contains(paragraphs[1].Marks, mark => mark is CommentMark { Edge: MarkEdge.End } end && end.CommentId == comment.Id);
        Assert.Contains(paragraphs[2].Marks, mark => mark is BookmarkMark { Name: "segno" });

        var written = new MemoryStream();
        _composer.Writer(DocxFlavour.Document).Write(document, written);
        written.Position = 0;
        using var reopened = WordprocessingDocument.Open(written, false);
        Assert.Single(reopened.MainDocumentPart!.Document!.Descendants<W.CommentRangeEnd>());
        Assert.Single(reopened.MainDocumentPart.Document.Descendants<W.CommentReference>());
    }

    [Fact]
    public void Un_gruppo_di_forme_con_tabelle_nel_testo_resta_un_gruppo_in_DOCX_e_ODT()
    {
        const string shape = "<wps:wsp><wps:cNvSpPr/><wps:spPr><a:xfrm><a:off x=\"{0}\" y=\"0\"/><a:ext cx=\"1000\" cy=\"2000\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom>"
            + "<a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill></wps:spPr><wps:txbx><w:txbxContent><w:tbl><w:tr><w:tc><w:p><w:r><w:t>cella</w:t></w:r></w:p></w:tc></w:tr></w:tbl><w:p/></w:txbxContent></wps:txbx><wps:bodyPr/></wps:wsp>";
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace} xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" "
                + "xmlns:wpg=\"http://schemas.microsoft.com/office/word/2010/wordprocessingGroup\" xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"><w:body><w:p><w:r><w:drawing>"
                + "<wp:anchor distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" simplePos=\"0\" relativeHeight=\"0\" behindDoc=\"0\" locked=\"0\" layoutInCell=\"1\" allowOverlap=\"1\"><wp:simplePos x=\"0\" y=\"0\"/>"
                + "<wp:positionH relativeFrom=\"column\"><wp:posOffset>0</wp:posOffset></wp:positionH><wp:positionV relativeFrom=\"paragraph\"><wp:posOffset>0</wp:posOffset></wp:positionV>"
                + "<wp:extent cx=\"6350000\" cy=\"6350000\"/><wp:wrapNone/><wp:docPr id=\"1\" name=\"Gruppo\"/><a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingGroup\"><wpg:wgp><wpg:cNvGrpSpPr/>"
                + "<wpg:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"2000\" cy=\"2000\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"2000\" cy=\"2000\"/></a:xfrm></wpg:grpSpPr>"
                + string.Format(System.Globalization.CultureInfo.InvariantCulture, shape, 0) + string.Format(System.Globalization.CultureInfo.InvariantCulture, shape, 1000)
                + "</wpg:wgp></a:graphicData></a:graphic></wp:anchor></w:drawing></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code is "DOCX181" or "DOCX124");
        AssertGroupTables(read.Value!);

        var docx = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(read.Value!, docx).Succeeded);
        docx.Position = 0;
        using (var written = WordprocessingDocument.Open(docx, false))
            Assert.Equal(2, written.MainDocumentPart!.Document!.Descendants<DocumentFormat.OpenXml.Office2010.Word.DrawingGroup.WordprocessingGroup>().Single().Descendants<W.Table>().Count());
        docx.Position = 0;
        AssertGroupTables(_composer.Reader(DocxFlavour.Document).Read(docx).Value!);

        var odt = new MemoryStream();
        Assert.True(new PulseOffice.Formats.Odf.Odt.OdtWriter().Write(read.Value!, odt).Succeeded);
        odt.Position = 0;
        using (var zip = new System.IO.Compression.ZipArchive(odt, System.IO.Compression.ZipArchiveMode.Read, true))
        using (var entry = zip.GetEntry("content.xml")!.Open())
        {
            System.Xml.Linq.XNamespace drawNs = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
            System.Xml.Linq.XNamespace tableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
            var group = System.Xml.Linq.XDocument.Load(entry).Descendants(drawNs + "g").Single();
            Assert.Equal(2, group.Elements(drawNs + "frame").Count(frame => frame.Element(drawNs + "text-box")?.Element(tableNs + "table") is not null));
        }
        odt.Position = 0;
        AssertGroupTables(new PulseOffice.Formats.Odf.Odt.OdtReader().Read(odt).Value!);
    }

    private static void AssertGroupTables(TextDocument document)
    {
        var group = Assert.Single(document.Objects.All);
        var content = Assert.IsType<PulseOffice.Documents.Objects.EmbeddedContent>(group.Content);
        var shapes = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeGroupObject>(content.Value);
        Assert.Equal(2, shapes.Children.Count);
        Assert.Equal(2, content.MemberStories.Count);
        Assert.All(content.MemberStories.Values, story => Assert.Single(story.Blocks.OfType<TableBlock>()));
    }

    [Fact]
    public void Le_colonne_dell_indice_analitico_attraversano_DOCX()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r>"
                + "<w:r><w:instrText xml:space=\"preserve\"> INDEX \\c \"2\" \\z \"1033\" </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>voce</w:t></w:r></w:p>"
                + "<w:p><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var document = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var index = Assert.Single(document.Body.Blocks.OfType<SectionBlock>());
        Assert.Equal(2, index.Columns.Count);

        var written = new MemoryStream();
        _composer.Writer(DocxFlavour.Document).Write(document, written);
        written.Position = 0;
        using var reopened = WordprocessingDocument.Open(written, false);
        Assert.Contains(reopened.MainDocumentPart!.Document!.Descendants<W.FieldCode>(), code => code.Text.Contains("INDEX \\c \"2\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Un_indice_in_un_campo_semplice_e_una_bibliografia_con_tabella_restano_indici_in_DOCX()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>"
                + "<w:p><w:fldSimple w:instr=\" TOC \\o &quot;1-3&quot; \\h \\z \\u \"><w:r><w:t>Nessuna voce</w:t></w:r></w:fldSimple></w:p>"
                + "<w:p><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> BIBLIOGRAPHY </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r></w:p>"
                + "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>Fonte</w:t></w:r></w:p></w:tc></w:tr></w:tbl>"
                + "<w:p><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var document = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var indexes = document.Body.Blocks.OfType<SectionBlock>().Select(section => section.Index!.Kind).ToList();
        Assert.Equal([PulseOffice.Documents.Indexes.IndexKind.Contents, PulseOffice.Documents.Indexes.IndexKind.Bibliography], indexes);

        var written = new MemoryStream();
        _composer.Writer(DocxFlavour.Document).Write(document, written);
        written.Position = 0;
        using var reopened = WordprocessingDocument.Open(written, false);
        var codes = reopened.MainDocumentPart!.Document!.Descendants<W.FieldCode>().Select(code => code.Text.Trim()).ToList();
        Assert.Contains(codes, code => code.StartsWith("TOC", StringComparison.Ordinal));
        Assert.Contains("BIBLIOGRAPHY", codes);
        Assert.Single(reopened.MainDocumentPart.Document.Descendants<W.Table>());
    }

    [Fact]
    public void Le_celle_avvolte_in_un_controllo_e_le_larghezze_decimali_si_leggono_e_le_celle_registrate_si_dichiarano()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body><w:tbl><w:tblGrid><w:gridCol w:w=\"2256.5\"/><w:gridCol w:w=\"2256.5\"/></w:tblGrid>"
                + "<w:tr><w:tc><w:p><w:r><w:t>a</w:t></w:r></w:p></w:tc><w:sdt><w:sdtPr><w:tag w:val=\"goog_rdk_1\"/></w:sdtPr><w:sdtContent><w:tc><w:tcPr><w:cellIns w:id=\"0\" w:author=\"a\"/></w:tcPr>"
                + "<w:p><w:ins w:id=\"1\" w:author=\"a\"><w:r><w:t>b</w:t></w:r></w:ins></w:p></w:tc></w:sdtContent></w:sdt></w:tr></w:tbl><w:p/></w:body></w:document>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        var table = Assert.Single(read.Value!.Body.Blocks.OfType<TableBlock>());
        Assert.Equal(2, table.Rows[0].Cells.Count);
        Assert.Equal("b", table.Rows[0].Cells[1].Content.Blocks.OfType<ParagraphBlock>().Single().Text);
        Assert.Equal(2257, table.Columns[0].Value);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX121");
    }

    [Fact]
    public void I_paragrafi_eliminati_con_il_loro_segno_diventano_una_eliminazione_che_li_contiene()
    {
        const string deleted = "<w:del w:id=\"{0}\" w:author=\"a\" w:date=\"2018-09-14T08:28:00Z\"/>";
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>"
                + "<w:p><w:pPr><w:rPr>" + string.Format(System.Globalization.CultureInfo.InvariantCulture, deleted, 1) + "</w:rPr></w:pPr></w:p>"
                + "<w:p><w:pPr><w:rPr>" + string.Format(System.Globalization.CultureInfo.InvariantCulture, deleted, 2) + "</w:rPr></w:pPr><w:del w:id=\"3\" w:author=\"a\"><w:r><w:delText>Lorem</w:delText></w:r></w:del></w:p>"
                + "<w:p><w:pPr><w:rPr>" + string.Format(System.Globalization.CultureInfo.InvariantCulture, deleted, 4) + "</w:rPr></w:pPr><w:r><w:t>resta</w:t></w:r></w:p>"
                + "<w:p><w:r><w:t>fine</w:t></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        var document = read.Value!;
        var paragraphs = document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal(["restafine"], paragraphs.Select(paragraph => paragraph.Text));
        var marks = paragraphs[0].Marks.OfType<ChangeMark>().ToList();
        Assert.Equal([0, 5], marks.Select(mark => mark.Offset));
        var deletions = marks.Select(mark => Assert.IsType<PulseOffice.Documents.Annotations.Deletion>(document.Changes.Find(mark.ChangeId))).ToList();
        Assert.Equal(["", "Lorem", ""], deletions[0].Removed.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
        Assert.Equal(["", ""], deletions[1].Removed.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
        Assert.Equal(2, document.Changes.All.Count);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX150");
    }

    [Fact]
    public void I_campi_HYPERLINK_diventano_collegamenti_e_i_campi_sconosciuti_si_dichiarano()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body><w:p>"
                + "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> HYPERLINK \"https://example.org/\" </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>"
                + "<w:r><w:t>sito</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r>"
                + "<w:fldSimple w:instr=\" HYPERLINK \\l &quot;capitolo&quot; \"><w:r><w:t>qui</w:t></w:r></w:fldSimple>"
                + "<w:fldSimple w:instr=\" SECTIONPAGES \"><w:r><w:t>3</w:t></w:r></w:fldSimple>"
                + "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> DOCPROPERTY marchio </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        var runs = read.Value!.Body.Blocks.OfType<ParagraphBlock>().Single().Content.Runs;
        Assert.Equal("https://example.org/", runs.Single(run => run.Text == "sito").Hyperlink);
        Assert.Equal("#capitolo", runs.Single(run => run.Text == "qui").Hyperlink);
        Assert.Null(runs.Single(run => run.Text == "3").Hyperlink);
        Assert.Contains(runs, run => run.Field == PulseOffice.Text.Rich.TextFieldKind.CustomProperty && run.FieldArgument == "marchio");
        Assert.Contains(runs, run => run.Field == PulseOffice.Text.Rich.TextFieldKind.Preserved && run.FieldArgument == " SECTIONPAGES " && run.Text == "3");
    }

    [Fact]
    public void Un_gruppo_annidato_in_un_gruppo_DOCX_non_si_perde()
    {
        const string shape = "<wps:wsp><wps:cNvSpPr/><wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"1000\" cy=\"1000\"/></a:xfrm><a:prstGeom prst=\"ellipse\"><a:avLst/></a:prstGeom></wps:spPr><wps:bodyPr/></wps:wsp>";
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace} xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" "
                + "xmlns:wpg=\"http://schemas.microsoft.com/office/word/2010/wordprocessingGroup\" xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"><w:body><w:p><w:r><w:drawing>"
                + "<wp:inline><wp:extent cx=\"635000\" cy=\"635000\"/><wp:docPr id=\"1\" name=\"Gruppo\"/><a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingGroup\"><wpg:wgp><wpg:cNvGrpSpPr/>"
                + "<wpg:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"2000\" cy=\"2000\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"2000\" cy=\"2000\"/></a:xfrm></wpg:grpSpPr>" + shape
                + "<wpg:grpSp><wpg:cNvGrpSpPr/><wpg:grpSpPr><a:xfrm><a:off x=\"1000\" y=\"1000\"/><a:ext cx=\"1000\" cy=\"1000\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"1000\" cy=\"1000\"/></a:xfrm></wpg:grpSpPr>" + shape + shape + "</wpg:grpSp>"
                + "</wpg:wgp></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var document = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var group = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeGroupObject>(((PulseOffice.Documents.Objects.EmbeddedContent)Assert.Single(document.Objects.All).Content).Value);
        Assert.Equal(2, group.Children.Count);
        Assert.Equal(2, Assert.IsType<PulseOffice.Drawing.Shapes.ShapeGroupObject>(group.Children[1].Content).Children.Count);
    }

    [Fact]
    public void Le_intestazioni_di_una_sezione_continua_valgono_dalla_pagina_che_inizia_e_si_ereditano()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var first = main.AddNewPart<HeaderPart>();
            first.Header = new W.Header($"<w:hdr {Namespace}><w:p><w:r><w:t>prima</w:t></w:r></w:p></w:hdr>");
            var second = main.AddNewPart<HeaderPart>();
            second.Header = new W.Header($"<w:hdr {Namespace}><w:p><w:r><w:t>seconda</w:t></w:r></w:p></w:hdr>");
            const string relationships = "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
            main.Document = new W.Document($"<w:document {Namespace} {relationships}><w:body>"
                + $"<w:p><w:pPr><w:sectPr><w:headerReference w:type=\"default\" r:id=\"{main.GetIdOfPart(first)}\"/></w:sectPr></w:pPr><w:r><w:t>uno</w:t></w:r></w:p>"
                + "<w:p><w:r><w:t>stessa pagina</w:t></w:r></w:p><w:p><w:pPr><w:pageBreakBefore/></w:pPr><w:r><w:t>nuova pagina</w:t></w:r></w:p>"
                + $"<w:p><w:r><w:t>ultima</w:t></w:r></w:p><w:sectPr><w:type w:val=\"continuous\"/><w:headerReference w:type=\"default\" r:id=\"{main.GetIdOfPart(second)}\"/></w:sectPr></w:body></w:document>");
        }

        stream.Position = 0;
        var document = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var paragraphs = document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Null(paragraphs[1].Content.PageStyleName);
        var name = Assert.IsType<string>(paragraphs[2].Content.PageStyleName);
        var style = document.Styles.Page.Find(name)!;
        Assert.Equal("seconda", style.Header.Default.Blocks.OfType<ParagraphBlock>().Single().Text);
    }

    [Fact]
    public void Il_testo_spostato_via_scritto_senza_delText_resta_nell_eliminazione_e_le_immagini_eliminate_si_dichiarano()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:moveFrom w:id=\"1\" w:author=\"a\"><w:r><w:t>spostato</w:t></w:r></w:moveFrom>"
                + "<w:del w:id=\"2\" w:author=\"a\"><w:r><w:drawing/></w:r></w:del></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        var deletion = Assert.IsType<PulseOffice.Documents.Annotations.Deletion>(Assert.Single(read.Value!.Changes.All));
        Assert.Equal("spostato", deletion.Removed.Blocks.OfType<ParagraphBlock>().Single().Text);
    }

    [Fact]
    public void Una_tabella_scritta_dentro_un_paragrafo_si_legge_dopo_il_paragrafo()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body><w:tbl><w:tr><w:tc><w:p><w:r><w:t>fuori</w:t></w:r>"
                + "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>dentro</w:t></w:r></w:p></w:tc></w:tr></w:tbl></w:p></w:tc></w:tr></w:tbl><w:p/></w:body></w:document>");
        }

        stream.Position = 0;
        var document = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var cell = Assert.Single(document.Body.Blocks.OfType<TableBlock>()).Rows[0].Cells[0].Content.Blocks;
        Assert.Equal("fuori", Assert.IsType<ParagraphBlock>(cell[0]).Text);
        var inner = Assert.IsType<TableBlock>(cell[1]);
        Assert.Equal("dentro", inner.Rows[0].Cells[0].Content.Blocks.OfType<ParagraphBlock>().Single().Text);
    }

    [Fact]
    public void Le_note_con_identificativi_che_non_corrispondono_ai_richiami_si_assegnano_in_ordine_di_identificativo()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body>"
                + "<w:p><w:r><w:t>a</w:t></w:r><w:r><w:footnoteReference w:id=\"0\"/></w:r></w:p>"
                + "<w:p><w:r><w:t>b</w:t></w:r><w:r><w:footnoteReference w:id=\"1\"/></w:r></w:p>"
                + "<w:p><w:r><w:t>c</w:t></w:r><w:r><w:footnoteReference w:id=\"2\"/></w:r></w:p></w:body></w:document>");
            main.AddNewPart<FootnotesPart>().Footnotes = new W.Footnotes($"<w:footnotes {Namespace}>"
                + "<w:footnote w:id=\"40\"><w:p><w:r><w:t>terza</w:t></w:r></w:p></w:footnote>"
                + "<w:footnote w:id=\"-1\"><w:p><w:r><w:t>prima</w:t></w:r></w:p></w:footnote>"
                + "<w:footnote w:id=\"1\"><w:p><w:r><w:t>seconda</w:t></w:r></w:p></w:footnote></w:footnotes>");
        }

        stream.Position = 0;
        var document = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var texts = document.Body.Blocks.OfType<ParagraphBlock>()
            .Select(paragraph => paragraph.Marks.OfType<NoteMark>().Single().NoteId)
            .Select(id => document.Notes.Find(id)!.Content.Blocks.OfType<ParagraphBlock>().Single().Text)
            .ToList();
        Assert.Equal(["prima", "seconda", "terza"], texts);
    }

    [Fact]
    public void I_segni_di_paragrafo_spostati_si_leggono_come_eliminati_e_inseriti()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>"
                + "<w:p><w:pPr><w:rPr><w:moveTo w:id=\"1\" w:author=\"a\" w:date=\"2021-07-23T13:38:00Z\"/></w:rPr></w:pPr><w:moveTo w:id=\"2\" w:author=\"a\" w:date=\"2021-07-23T13:38:00Z\"><w:r><w:t>arrivato</w:t></w:r></w:moveTo></w:p>"
                + "<w:p><w:r><w:t>mezzo</w:t></w:r></w:p>"
                + "<w:p><w:pPr><w:rPr><w:moveFrom w:id=\"3\" w:author=\"a\" w:date=\"2021-07-23T13:38:00Z\"/></w:rPr></w:pPr><w:moveFrom w:id=\"4\" w:author=\"a\" w:date=\"2021-07-23T13:38:00Z\"><w:r><w:t>arrivato</w:t></w:r></w:moveFrom></w:p>"
                + "<w:p><w:r><w:t>fine</w:t></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        var document = read.Value!;
        Assert.Equal(["arrivato", "mezzo", "fine"], document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
        var deletion = Assert.Single(document.Changes.All.OfType<PulseOffice.Documents.Annotations.Deletion>());
        Assert.Equal(["arrivato", ""], deletion.Removed.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX150");

        var written = new MemoryStream();
        _composer.Writer(DocxFlavour.Document).Write(document, written);
        written.Position = 0;
        using var again = WordprocessingDocument.Open(written, false);
        var first = again.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().First();
        Assert.NotNull(first.ParagraphProperties?.ParagraphMarkRunProperties?.GetFirstChild<W.Inserted>());
    }

    [Fact]
    public void Il_segno_eliminato_di_un_paragrafo_vuoto_prima_di_una_tabella_si_dichiara()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>"
                + "<w:p><w:pPr><w:rPr><w:del w:id=\"1\" w:author=\"a\"/></w:rPr></w:pPr></w:p>"
                + "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>cella</w:t></w:r></w:p></w:tc></w:tr></w:tbl>"
                + "<w:p><w:r><w:t>fine</w:t></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.IsType<ParagraphBlock>(read.Value!.Body.Blocks.First());
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX150");
    }

    [Theory]
    [InlineData("<w:p><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText> IF \"1\" = \"\" \"\" \"a</w:instrText></w:r></w:p><w:p><w:r><w:instrText>\" </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>a</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>", "DOCX151")]
    [InlineData("<w:p><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText> REF ID0 \\h </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r></w:p><w:tbl><w:tr><w:tc><w:p><w:r><w:t>ID</w:t></w:r></w:p></w:tc></w:tr></w:tbl><w:p><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>", "DOCX151")]
    [InlineData("<w:p><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText> ADDRESSBLOCK \\d </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>M</w:t></w:r></w:p><w:p><w:r><w:t>Ma</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>", "DOCX151")]
    public void I_campi_che_attraversano_i_paragrafi_restano_campi(string body, string code)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.NotNull(read.Value);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == code);
        Assert.Contains(read.Value.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks), mark => mark is PulseOffice.Documents.Marks.FieldCodeMark);
    }

    [Fact]
    public void Un_campo_in_un_solo_paragrafo_non_si_dichiara()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText> PAGE </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>1</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p></w:body></w:document>");

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code is "DOCX151" or "DOCX123");
    }

    [Theory]
    [InlineData("oddPage", PageUsage.Right)]
    [InlineData("evenPage", PageUsage.Left)]
    public void Le_sezioni_che_iniziano_su_pagina_dispari_o_pari_attraversano_DOCX(string type, PageUsage usage)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>"
                + "<w:p><w:pPr><w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/></w:sectPr></w:pPr><w:r><w:t>uno</w:t></w:r></w:p>"
                + $"<w:p><w:r><w:t>due</w:t></w:r></w:p><w:sectPr><w:type w:val=\"{type}\"/><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:vAlign w:val=\"center\"/></w:sectPr></w:body></w:document>");

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        var document = read.Value!;
        var second = document.Body.Blocks.OfType<ParagraphBlock>().Last();
        var starter = document.Styles.Page.Find(second.Content.PageStyleName!)!;
        Assert.Equal(usage, starter.Usage);
        Assert.Equal(PageUsage.All, document.Styles.Page.Find(starter.NextStyleName!)!.Usage);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX152");

        var writer = _composer.Writer(DocxFlavour.Document);
        Assert.DoesNotContain(writer.CheckFidelity(document).Losses, loss => loss.Code == "DOCX037");
        var written = new MemoryStream();
        writer.Write(document, written);
        written.Position = 0;
        using var again = WordprocessingDocument.Open(written, false);
        var last = again.MainDocumentPart!.Document!.Body!.Elements<W.SectionProperties>().Single();
        Assert.Equal(type, last.GetFirstChild<W.SectionType>()!.Val!.InnerText);
    }
}

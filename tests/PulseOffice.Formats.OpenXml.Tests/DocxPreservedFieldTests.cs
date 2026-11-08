using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxPreservedFieldTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Pulse = "urn:pulseoffice:text";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private MemoryStream Docx(string body)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
        stream.Position = 0;
        return stream;
    }

    private TextDocument ReadDocx(Stream stream)
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX123");
        return read.Value!;
    }

    private MemoryStream WriteDocx(TextDocument document)
    {
        var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    private MemoryStream WriteOdt(TextDocument document)
    {
        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    private TextDocument ReadOdt(Stream stream)
    {
        var read = new OdtReader().Read(stream);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODF082");
        return read.Value!;
    }

    private XDocument OdtContent(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
        using var entry = zip.GetEntry("content.xml")!.Open();
        var content = XDocument.Load(entry);
        stream.Position = 0;
        return content;
    }

    private IReadOnlyList<W.SimpleField> SimpleFields(Stream stream)
    {
        using var package = WordprocessingDocument.Open(stream, false);
        var found = package.MainDocumentPart!.Document!.Descendants<W.SimpleField>().Select(field => (W.SimpleField)field.CloneNode(true)).ToList();
        stream.Position = 0;
        return found;
    }

    private TextRun Preserved(TextDocument document) =>
        document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs).Single(run => run.Field == TextFieldKind.Preserved);

    private MemoryStream Odt(string paragraph)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using (var mime = new StreamWriter(zip.CreateEntry("mimetype", CompressionLevel.NoCompression).Open(), Encoding.ASCII)) mime.Write("application/vnd.oasis.opendocument.text");
            using var content = new StreamWriter(zip.CreateEntry("content.xml").Open(), Encoding.UTF8);
            content.Write($"""<office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" office:version="1.3"><office:body><office:text><text:p>{paragraph}</text:p></office:text></office:body></office:document-content>""");
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Un_campo_Word_sconosciuto_conserva_istruzione_e_risultato_in_DOCX()
    {
        var document = ReadDocx(Docx("<w:p><w:r><w:t xml:space=\"preserve\">Stampato il </w:t></w:r><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> PRINTDATE \\@ \"d MMMM yyyy\" </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>3 marzo 2024</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>"));
        var field = Preserved(document);
        Assert.Equal(" PRINTDATE \\@ \"d MMMM yyyy\" ", field.FieldArgument);
        Assert.Equal("3 marzo 2024", field.Text);

        var written = WriteDocx(document);
        var simple = Assert.Single(SimpleFields(written));
        Assert.Equal(" PRINTDATE \\@ \"d MMMM yyyy\" ", simple.Instruction?.Value);
        Assert.Null(simple.GetFirstChild<W.FieldData>());
        var again = Preserved(ReadDocx(written));
        Assert.Equal(field.FieldArgument, again.FieldArgument);
        Assert.Equal("3 marzo 2024", again.Text);
    }

    [Fact]
    public void Un_campo_Word_con_equivalente_ODF_diventa_il_campo_ODF_e_torna_identico()
    {
        var document = ReadDocx(Docx("<w:p><w:fldSimple w:instr=\" USERINITIALS \\* Upper \"><w:r><w:t>EG</w:t></w:r></w:fldSimple></w:p>"));
        var odt = WriteOdt(document);
        var element = OdtContent(odt).Descendants(Text + "author-initials").Single();
        Assert.Equal(" USERINITIALS \\* Upper ", (string?)element.Attribute(Pulse + "field-instruction"));
        Assert.Equal("EG", element.Value);

        var fromOdt = ReadOdt(odt);
        Assert.Null(Preserved(fromOdt).FieldDetail?.NativeOdf);
        var simple = Assert.Single(SimpleFields(WriteDocx(fromOdt)));
        Assert.Equal(" USERINITIALS \\* Upper ", simple.Instruction?.Value);
        Assert.Null(simple.GetFirstChild<W.FieldData>());
    }

    [Fact]
    public void Un_campo_Word_senza_equivalente_attraversa_ODT_in_uno_span_col_nostro_attributo()
    {
        var document = ReadDocx(Docx("<w:p><w:fldSimple w:instr=\" ADDRESSBLOCK \\f &quot;x&quot; \"><w:r><w:t>Via Roma 1</w:t></w:r></w:fldSimple></w:p>"));
        var odt = WriteOdt(document);
        var span = OdtContent(odt).Descendants(Text + "span").Single(item => item.Attribute(Pulse + "field-instruction") is not null);
        Assert.Equal("Via Roma 1", span.Value);

        var back = Preserved(ReadOdt(odt));
        Assert.Equal(" ADDRESSBLOCK \\f \"x\" ", back.FieldArgument);
        Assert.Equal("Via Roma 1", back.Text);
        Assert.Equal(" ADDRESSBLOCK \\f \"x\" ", Assert.Single(SimpleFields(WriteDocx(ReadOdt(odt)))).Instruction?.Value);
    }

    [Fact]
    public void Un_pulsante_macro_mostra_il_testo_dell_istruzione()
    {
        var document = ReadDocx(Docx("<w:p><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> MACROBUTTON NoMacro [Scrivi qui] </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>"));
        var field = Preserved(document);
        Assert.Equal("[Scrivi qui]", field.Text);
        var placeholder = OdtContent(WriteOdt(document)).Descendants(Text + "placeholder").Single();
        Assert.Equal("text", (string?)placeholder.Attribute(Text + "placeholder-type"));
        Assert.Equal("[Scrivi qui]", placeholder.Value);
    }

    [Fact]
    public void Un_campo_ODF_senza_equivalente_attraversa_DOCX_e_torna_con_l_elemento_nativo()
    {
        var document = ReadOdt(Odt("Scelta: <text:drop-down text:name=\"Colore\"><text:label text:value=\"Rosso\"/><text:label text:value=\"Verde\"/>Verde</text:drop-down>"));
        var field = Preserved(document);
        Assert.Equal("Verde", field.Text);
        Assert.NotNull(field.FieldDetail?.NativeOdf);

        var docx = WriteDocx(document);
        var simple = Assert.Single(SimpleFields(docx));
        Assert.Equal(" QUOTE \"Verde\" ", simple.Instruction?.Value);
        Assert.NotNull(simple.GetFirstChild<W.FieldData>());

        var fromDocx = ReadDocx(docx);
        var kept = Preserved(fromDocx);
        Assert.Null(kept.FieldArgument);
        Assert.Equal("Verde", kept.Text);
        var dropDown = OdtContent(WriteOdt(fromDocx)).Descendants(Text + "drop-down").Single();
        Assert.Equal("Colore", (string?)dropDown.Attribute(Text + "name"));
        Assert.Equal(["Rosso", "Verde"], dropDown.Elements(Text + "label").Select(label => (string?)label.Attribute(Text + "value")));
        Assert.Equal("Verde", dropDown.Value);
        Assert.Null(dropDown.Attribute(Pulse + "field-instruction"));
    }

    [Fact]
    public void Un_segnaposto_ODF_diventa_un_pulsante_macro_in_DOCX()
    {
        var document = ReadOdt(Odt("<text:placeholder text:placeholder-type=\"text\" text:description=\"Nome\">&lt;Nome&gt;</text:placeholder>"));
        var simple = Assert.Single(SimpleFields(WriteDocx(document)));
        Assert.Equal(" MACROBUTTON NoMacro <Nome> ", simple.Instruction?.Value);
        Assert.NotNull(simple.GetFirstChild<W.FieldData>());
        var back = Preserved(ReadDocx(WriteDocx(document)));
        Assert.Equal("<Nome>", back.Text);
        var placeholder = OdtContent(WriteOdt(ReadDocx(WriteDocx(document)))).Descendants(Text + "placeholder").Single();
        Assert.Equal("Nome", (string?)placeholder.Attribute(Text + "description"));
    }

    [Fact]
    public void An_ODF_input_field_keeps_its_prompt_in_the_Word_FILLIN_instruction()
    {
        var document = ReadOdt(Odt("<text:text-input text:description=\"Your &quot;name&quot;\">Ada</text:text-input>"));

        var simple = Assert.Single(SimpleFields(WriteDocx(document)));

        Assert.Equal(" FILLIN \"Your \\\"name\\\"\" ", simple.Instruction?.Value);
    }

    [Fact]
    public void A_Word_FILLIN_prompt_becomes_the_description_of_the_ODF_input_field()
    {
        var document = ReadDocx(Docx("<w:p><w:fldSimple w:instr=\" FILLIN &quot;Client name&quot; \\d &quot;Ada&quot; \"><w:r><w:t>Ada</w:t></w:r></w:fldSimple></w:p>"));

        var input = OdtContent(WriteOdt(document)).Descendants(Text + "text-input").Single();

        Assert.Equal("Client name", (string?)input.Attribute(Text + "description"));
        Assert.Equal("Ada", input.Value);
        Assert.Equal(" FILLIN \"Client name\" \\d \"Ada\" ", Preserved(ReadOdt(WriteOdt(document))).FieldArgument);
    }

    [Fact]
    public void Dati_di_campo_estranei_non_si_iniettano_in_ODT()
    {
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes("urn:pulseoffice:odf-field\n<draw:frame xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\"/>"));
        var document = ReadDocx(Docx($"<w:p><w:fldSimple w:instr=\" QUOTE &quot;x&quot; \"><w:fldData>{data}</w:fldData><w:r><w:t>x</w:t></w:r></w:fldSimple></w:p>"));
        var content = OdtContent(WriteOdt(document));
        Assert.Empty(content.Descendants(XName.Get("frame", "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0")));
        Assert.Equal("x", Preserved(ReadOdt(WriteOdt(document))).Text);
    }
}

using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxFieldRangeTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static readonly XNamespace Pulse = "urn:pulseoffice:text";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static string Begin => "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r>";

    private static string Separate => "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>";

    private static string End => "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";

    private static string Code(string text) => $"<w:r><w:instrText xml:space=\"preserve\">{text}</w:instrText></w:r>";

    private static string Text(string text) => $"<w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r>";

    private static string Paragraph(string content) => "<w:p>" + content + "</w:p>";

    private static MemoryStream Docx(string body)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
        stream.Position = 0;
        return stream;
    }

    private (TextDocument Document, IReadOnlyList<string> Codes) ReadDocx(Stream stream)
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return (read.Value!, [.. read.Diagnostics.Select(diagnostic => diagnostic.Code)]);
    }

    private MemoryStream WriteDocx(TextDocument document)
    {
        var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream WriteOdt(TextDocument document)
    {
        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    private static TextDocument ReadOdt(Stream stream)
    {
        var read = new OdtReader().Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    private static XDocument OdtContent(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
        using var entry = zip.GetEntry("content.xml")!.Open();
        var content = XDocument.Load(entry);
        stream.Position = 0;
        return content;
    }

    private static List<W.Paragraph> Written(Stream stream)
    {
        using var package = WordprocessingDocument.Open(stream, false);
        var paragraphs = package.MainDocumentPart!.Document!.Body!.Descendants<W.Paragraph>().Select(paragraph => (W.Paragraph)paragraph.CloneNode(true)).ToList();
        stream.Position = 0;
        return paragraphs;
    }

    private static int Chars(IEnumerable<W.Paragraph> paragraphs, W.FieldCharValues type) =>
        paragraphs.SelectMany(paragraph => paragraph.Descendants<W.FieldChar>()).Count(item => item.FieldCharType?.Value == type);

    private static string Instruction(IEnumerable<W.Paragraph> paragraphs) =>
        string.Concat(paragraphs.SelectMany(paragraph => paragraph.Descendants<W.FieldCode>()).Select(code => code.Text));

    private static List<FieldCodeMark> Marks(TextDocument document) =>
        [.. document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks).OfType<FieldCodeMark>()];

    private static string Visible(TextDocument document) =>
        string.Join("|", document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Content.Text));

    private const string SpanningIf = "<w:p>" + "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> IF \"1234\" = \"\" \"\" \"phone: 1234</w:instrText></w:r></w:p>"
        + "<w:p><w:r><w:instrText xml:space=\"preserve\">\" </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>phone: 1234</w:t></w:r></w:p>"
        + "<w:p><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>";

    [Fact]
    public void AFieldSpanningParagraphsIsReadAsARangeWithItsCachedResult()
    {
        var (document, codes) = ReadDocx(Docx(SpanningIf));
        Assert.DoesNotContain("DOCX151", codes);
        Assert.Equal("|phone: 1234|", Visible(document));
        var marks = Marks(document);
        Assert.Equal([FieldCodePart.Begin, FieldCodePart.Code, FieldCodePart.Separate, FieldCodePart.End], marks.Select(mark => mark.Part));
        Assert.Single(marks.Select(mark => mark.FieldId).Distinct());
        Assert.Equal(" IF \"1234\" = \"\" \"\" \"phone: 1234\" ", string.Concat(marks.Select(mark => mark.Code)));
    }

    [Fact]
    public void FieldDataOfARangeIsKeptOnlyWhenItIsBoundedBase64()
    {
        string WithData(string data) => SpanningIf.Replace("<w:fldChar w:fldCharType=\"begin\"/>", $"<w:fldChar w:fldCharType=\"begin\"><w:fldData xml:space=\"preserve\">{data}</w:fldData></w:fldChar>", StringComparison.Ordinal);
        var small = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        Assert.Equal(small, Marks(ReadDocx(Docx(WithData(small))).Document)[0].Data);
        Assert.Null(Marks(ReadDocx(Docx(WithData(new string('A', FieldCodeMark.DataLimit + 4)))).Document)[0].Data);
        Assert.Null(Marks(ReadDocx(Docx(WithData("not base64 at all!"))).Document)[0].Data);
    }

    [Fact]
    public void FieldDataOfAnOdtFieldCodeIsKeptOnlyWhenItIsBoundedBase64()
    {
        var (document, _) = ReadDocx(Docx(SpanningIf));
        var odt = OdtContent(WriteOdt(document));
        var begin = odt.Descendants(Pulse + "field-code").First(element => (string?)element.Attribute(Pulse + "part") == "begin");
        begin.SetAttributeValue(Pulse + "data", "<not base64>");
        var stream = WriteOdt(document);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            zip.GetEntry("content.xml")!.Delete();
            using var entry = zip.CreateEntry("content.xml").Open();
            odt.Save(entry);
        }
        stream.Position = 0;
        Assert.Null(Marks(ReadOdt(stream))[0].Data);
    }

    [Fact]
    public void AFieldSpanningParagraphsKeepsItsParagraphLayoutThroughDocx()
    {
        var (document, _) = ReadDocx(Docx(SpanningIf));
        var paragraphs = Written(WriteDocx(document));
        Assert.Equal(1, Chars(paragraphs, W.FieldCharValues.Begin));
        Assert.Equal(1, Chars(paragraphs, W.FieldCharValues.Separate));
        Assert.Equal(1, Chars(paragraphs, W.FieldCharValues.End));
        Assert.Equal(" IF \"1234\" = \"\" \"\" \"phone: 1234", Instruction([paragraphs[0]]));
        Assert.Equal("\" ", Instruction([paragraphs[1]]));
        Assert.Single(paragraphs[2].Descendants<W.FieldChar>());
        var (back, codes) = ReadDocx(WriteDocx(document));
        Assert.DoesNotContain("DOCX151", codes);
        Assert.Equal(Marks(document).Select(mark => (mark.Part, mark.Code)), Marks(back).Select(mark => (mark.Part, mark.Code)));
    }

    [Fact]
    public void AFieldSpanningParagraphsCrossesOdtAsForeignMarks()
    {
        var (document, _) = ReadDocx(Docx(SpanningIf));
        var odt = WriteOdt(document);
        var written = OdtContent(odt).Descendants(Pulse + "field-code").ToList();
        Assert.Equal(["begin", "code", "separate", "end"], written.Select(item => (string?)item.Attribute(Pulse + "part")));
        var back = ReadOdt(odt);
        Assert.Equal("|phone: 1234|", Visible(back));
        Assert.Equal(Marks(document).Select(mark => (mark.Part, mark.Code)), Marks(back).Select(mark => (mark.Part, mark.Code)));
        var paragraphs = Written(WriteDocx(back));
        Assert.Equal(" IF \"1234\" = \"\" \"\" \"phone: 1234\" ", Instruction(paragraphs));
        Assert.Equal(1, Chars(paragraphs, W.FieldCharValues.End));
    }

    [Fact]
    public void AFieldNestedInAResultStaysAFieldOfItsOwn()
    {
        var (document, codes) = ReadDocx(Docx(Paragraph(Begin + Code(" IF 1 = 1 \"sì\" \"no\" ") + Separate + Begin + Code(" MERGEFIELD Nome ") + Separate + Text("Ada") + End + End)));
        Assert.DoesNotContain("DOCX161", codes);
        var inner = document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs).Single(run => run.Field != TextFieldKind.None);
        Assert.Equal("Ada", inner.Text);
        Assert.Equal(TextFieldKind.Database, inner.Field);
        var paragraphs = Written(WriteDocx(document));
        Assert.Equal(2, Chars(paragraphs, W.FieldCharValues.Begin) + paragraphs.Sum(paragraph => paragraph.Descendants<W.SimpleField>().Count()));
        Assert.Equal(Chars(paragraphs, W.FieldCharValues.Begin), Chars(paragraphs, W.FieldCharValues.End));
        Assert.StartsWith(" IF 1 = 1 \"sì\" \"no\" ", Instruction(paragraphs), StringComparison.Ordinal);
    }

    [Fact]
    public void FieldsNestedInASpanningInstructionAreKeptInTheCode()
    {
        var body = Paragraph(Begin + Code(" QUOTE \"a ")) + Paragraph(Begin + Code(" QUOTE \"x\" ") + Separate + Text("x") + End + Code("\" ") + Separate + Text("a x") + End);
        var (document, codes) = ReadDocx(Docx(body));
        Assert.DoesNotContain("DOCX151", codes);
        Assert.DoesNotContain("DOCX160", codes);
        Assert.Contains(Marks(document), mark => mark.Code.Contains(FieldCodeMark.NestedBegin));
        var paragraphs = Written(WriteDocx(document));
        Assert.Equal(2, Chars(paragraphs, W.FieldCharValues.Begin));
        Assert.Equal(2, Chars(paragraphs, W.FieldCharValues.Separate));
        Assert.Equal(" QUOTE \"a  QUOTE \"x\" \" ", Instruction(paragraphs));
        Assert.Equal("xa x", string.Concat(paragraphs.SelectMany(paragraph => paragraph.Descendants<W.Text>()).Select(text => text.Text)));
    }

    [Fact]
    public void LiteralNestingMarkersInAnInstructionDoNotForgeNestedFields()
    {
        var body = Paragraph(Begin + Code(" QUOTE \"a\uE000b ")) + Paragraph(Code("\" ") + Separate + Text("ab") + End);
        var (document, _) = ReadDocx(Docx(body));
        Assert.DoesNotContain(Marks(document), mark => mark.Code.Contains(FieldCodeMark.NestedBegin));
        var paragraphs = Written(WriteDocx(document));
        Assert.Equal(1, Chars(paragraphs, W.FieldCharValues.Begin));
        Assert.Equal(1, Chars(paragraphs, W.FieldCharValues.End));
    }

    [Fact]
    public void AnUnclosedNestedMarkerIsEndedBeforeTheOuterField()
    {
        var (document, _) = ReadDocx(Docx(SpanningIf));
        var first = document.Body.Blocks.OfType<ParagraphBlock>().First();
        first.Marks = [.. first.Marks.Select(mark => mark is FieldCodeMark { Part: FieldCodePart.Begin } begin ? begin with { Code = begin.Code + FieldCodeMark.NestedBegin + " PAGE " } : mark)];
        var paragraphs = Written(WriteDocx(document));
        Assert.Equal(2, Chars(paragraphs, W.FieldCharValues.Begin));
        Assert.Equal(2, Chars(paragraphs, W.FieldCharValues.End));
        var order = paragraphs.SelectMany(paragraph => paragraph.Descendants<W.FieldChar>()).Select(item => item.FieldCharType!.Value).ToList();
        Assert.True(order.IndexOf(W.FieldCharValues.End) < order.IndexOf(W.FieldCharValues.Separate));
    }

    [Fact]
    public void AnUnbalancedRangeIsNotWrittenAsAField()
    {
        var (document, _) = ReadDocx(Docx(SpanningIf));
        var last = document.Body.Blocks.OfType<ParagraphBlock>().Last();
        last.Marks = [.. last.Marks.Where(mark => mark is not FieldCodeMark)];
        var paragraphs = Written(WriteDocx(document));
        Assert.Empty(paragraphs.SelectMany(paragraph => paragraph.Descendants<W.FieldChar>()));
        Assert.Contains(paragraphs, paragraph => paragraph.InnerText == "phone: 1234");
    }

    [Fact]
    public void AFieldWhoseResultHoldsATableIsARangeAroundTheTable()
    {
        var table = "<w:tbl><w:tblGrid><w:gridCol w:w=\"2000\"/></w:tblGrid><w:tr><w:tc><w:p><w:r><w:t>cella</w:t></w:r></w:p></w:tc></w:tr></w:tbl>";
        var body = Paragraph(Begin + Code(" REF ID0 \\h ") + Separate) + table + Paragraph(Text("dopo") + End);
        var (document, codes) = ReadDocx(Docx(body));
        Assert.DoesNotContain("DOCX151", codes);
        Assert.Contains(document.Body.Blocks, block => block is TableBlock);
        var paragraphs = Written(WriteDocx(document));
        Assert.Equal(1, Chars(paragraphs, W.FieldCharValues.Begin));
        Assert.Equal(1, Chars(paragraphs, W.FieldCharValues.End));
    }
}

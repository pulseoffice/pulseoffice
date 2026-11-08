using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxIndexRangeAndDeletedFieldTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument NewDocument() => new(new DefaultStyleSheetFactory().Create());

    private MemoryStream Write(TextDocument document)
    {
        var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    private PulseOffice.Foundation.Diagnostics.OperationResult<TextDocument> Read(Stream stream) => _composer.Reader(DocxFlavour.Document).Read(stream);

    private string Body(Stream stream)
    {
        using var package = WordprocessingDocument.Open(stream, false);
        var xml = package.MainDocumentPart!.Document!.Body!.OuterXml;
        stream.Position = 0;
        return xml;
    }

    [Fact]
    public void Una_voce_d_indice_su_un_intervallo_attraversa_DOCX_come_segnalibro_e_XE_r()
    {
        var document = NewDocument();
        document.Body.Replace(
        [
            new ParagraphBlock("p1", "Primo paragrafo") { Marks = [new IndexEntryMark(6, "Voce", MarkEdge.Start) { Key1 = "Capo" }] },
            new ParagraphBlock("p2", "Secondo paragrafo") { Marks = [new IndexEntryMark(7, "Voce", MarkEdge.End) { Key1 = "Capo" }] },
        ]);

        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX008");
        using var stream = Write(document);
        var xml = Body(stream);
        Assert.Contains("w:name=\"_Idx1\"", xml);
        Assert.Contains("XE &quot;Capo:Voce&quot; \\r _Idx1", xml);
        var read = Read(stream).Value!;
        var paragraphs = new BlockTree().Paragraphs(read.Body).ToList();
        var start = Assert.Single(paragraphs[0].Marks.OfType<IndexEntryMark>());
        Assert.Equal((6, MarkEdge.Start, "Voce", "Capo"), (start.Offset, start.Edge, start.Entry, start.Key1));
        var end = Assert.Single(paragraphs[1].Marks.OfType<IndexEntryMark>());
        Assert.Equal((7, MarkEdge.End), (end.Offset, end.Edge));
        Assert.DoesNotContain(paragraphs.SelectMany(paragraph => paragraph.Marks), mark => mark is BookmarkMark);
    }

    [Fact]
    public void Una_XE_di_Word_con_un_segnalibro_visibile_diventa_un_intervallo_e_il_segnalibro_resta_se_e_citato()
    {
        var body = "<w:p><w:r><w:t>Uno </w:t></w:r><w:bookmarkStart w:id=\"1\" w:name=\"Tratto\"/><w:fldSimple w:instr=\" XE &quot;Mare&quot; \\r Tratto \"/>"
                   + "<w:r><w:t>due</w:t></w:r><w:bookmarkEnd w:id=\"1\"/></w:p>"
                   + "<w:p><w:fldSimple w:instr=\" PAGEREF Tratto \\h \"><w:r><w:t>1</w:t></w:r></w:fldSimple></w:p>";
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
        stream.Position = 0;

        var first = new BlockTree().Paragraphs(Read(stream).Value!.Body).First();

        var entries = first.Marks.OfType<IndexEntryMark>().ToList();
        Assert.Equal([(4, MarkEdge.Start), (7, MarkEdge.End)], entries.Select(entry => (entry.Offset, entry.Edge)));
        Assert.Equal(2, first.Marks.OfType<BookmarkMark>().Count(mark => mark.Name == "Tratto"));
    }

    private TextDocument WithDeletedField(TextRun run)
    {
        var document = NewDocument();
        document.Body.Replace([new ParagraphBlock("p1", "Corpo") { Marks = [new ChangeMark(5, "ct1", MarkEdge.Point)] }]);
        var removed = new TextParagraph(string.Empty).WithRuns([new TextRun("prima "), run]);
        document.Changes.Add(new Deletion("ct1", "Anna", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("px", removed)])));
        return document;
    }

    [Fact]
    public void Un_campo_cancellato_va_in_DOCX_con_delInstrText_e_torna_campo()
    {
        var document = WithDeletedField(new TextRun("Anna Rossi") { Field = TextFieldKind.Author });

        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX195");
        using var stream = Write(document);
        var xml = Body(stream);
        Assert.Contains("<w:delInstrText", xml);
        Assert.DoesNotContain("<w:fldSimple", xml);
        var read = Read(stream);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX117");
        var deletion = Assert.Single(read.Value!.Changes.All.OfType<Deletion>());
        var runs = deletion.Removed.Blocks.OfType<ParagraphBlock>().Single().Content.Runs;
        Assert.Equal("prima Anna Rossi", string.Concat(runs.Select(run => run.Text)));
        var field = Assert.Single(runs, run => run.Field != TextFieldKind.None);
        Assert.Equal((TextFieldKind.Author, "Anna Rossi"), (field.Field, field.Text));
    }

    [Fact]
    public void Un_campo_preservato_cancellato_conserva_istruzione_e_dati()
    {
        var document = WithDeletedField(new TextRun("12") { Field = TextFieldKind.Preserved, FieldArgument = " PRINTDATE \\@ \"d\" " });

        using var stream = Write(document);
        var read = Read(stream).Value!;

        var field = read.Changes.All.OfType<Deletion>().Single().Removed.Blocks.OfType<ParagraphBlock>().Single().Content.Runs.Single(run => run.Field != TextFieldKind.None);
        Assert.Equal((TextFieldKind.Preserved, " PRINTDATE \\@ \"d\" ", "12"), (field.Field, field.FieldArgument, field.Text));
    }

    [Fact]
    public void Un_campo_che_Word_non_ha_dentro_una_cancellazione_resta_dichiarato()
    {
        var document = WithDeletedField(new TextRun("3") { Field = TextFieldKind.ParagraphCount });

        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX195");
    }

    public static TheoryData<IndexDefinition> Settings() =>
    [
        new IndexDefinition(IndexKind.Alphabetical) { CaseSensitive = true, CombineEntries = false, PageNumbers = false, LetterSeparators = true },
        new IndexDefinition(IndexKind.Contents) { FromOutline = false, Levels = 10, Hyperlinks = false },
        new IndexDefinition(IndexKind.Illustrations) { Sequence = "Figure", Captions = CaptionDisplay.CategoryAndNumber },
        new IndexDefinition(IndexKind.User) { UserName = "Piante", Levels = 4, ObjectSources = IndexObjectSources.Charts },
        new IndexDefinition(IndexKind.Bibliography) { SortByPosition = false },
    ];

    [Theory]
    [MemberData(nameof(Settings))]
    public void Le_impostazioni_degli_indici_attraversano_DOCX(IndexDefinition index)
    {
        var document = NewDocument();
        document.Body.Replace(
        [
            new SectionBlock("ix", "Indice", new BlockContainer([new ParagraphBlock("e", "Voce\t1")])) { Index = index },
            new ParagraphBlock("p", "Testo"),
        ]);

        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX025");
        using var stream = Write(document);
        var read = Read(stream).Value!;

        var section = Assert.Single(new BlockTree().Blocks(read.Body).OfType<SectionBlock>());
        Assert.Equal(index, section.Index);
    }

    [Fact]
    public void Dati_di_indice_estranei_non_cambiano_l_indice()
    {
        var body = "<w:p><w:r><w:fldChar w:fldCharType=\"begin\"><w:fldData>bm90IG91cnM=</w:fldData></w:fldChar></w:r><w:r><w:instrText> INDEX \\h \"A\" </w:instrText></w:r>"
                   + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>Voce</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>";
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
        stream.Position = 0;

        var section = Assert.Single(new BlockTree().Blocks(Read(stream).Value!.Body).OfType<SectionBlock>());

        Assert.Equal(new IndexDefinition(IndexKind.Alphabetical) { LetterSeparators = true, Hyperlinks = false }, section.Index);
    }
}

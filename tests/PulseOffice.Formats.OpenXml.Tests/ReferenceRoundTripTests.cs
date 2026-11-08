using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ReferenceRoundTripTests
{
    private const string Folder = "Fixtures/References";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly ReferenceSample _sample = new();

    public static TheoryData<string> Routes() => new() { "odt", "docx", "odt-lo", "docx-lo", "odt-lo-docx", "docx-lo-odt" };

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private TextDocument Fixture(string name, ITextDocumentReader reader)
    {
        using var stream = File.OpenRead(Path.Combine(Folder, name));
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private TextDocument Route(string route) => route switch
    {
        "odt" => Through(_sample.Build(), new OdtWriter(), new OdtReader()),
        "docx" => Through(_sample.Build(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)),
        "odt-lo" => Fixture("references.lo.odt", new OdtReader()),
        "docx-lo" => Fixture("references.lo.docx", _composer.Reader(DocxFlavour.Document)),
        "odt-lo-docx" => Fixture("references.odt.lo.docx", _composer.Reader(DocxFlavour.Document)),
        _ => Fixture("references.docx.lo.odt", new OdtReader()),
    };

    private bool Docx(string route) => route.Contains("docx", StringComparison.Ordinal);

    private bool ThroughReferenceSuite(string route) => route.Contains("lo", StringComparison.Ordinal);

    private ParagraphBlock Paragraph(TextDocument document, string start) =>
        document.Body.Blocks.OfType<ParagraphBlock>().First(paragraph => paragraph.Text.StartsWith(start, StringComparison.Ordinal));

    private IEnumerable<TextRun> Fields(ParagraphBlock paragraph, TextFieldKind kind) => paragraph.Content.Runs.Where(run => run.Field == kind);

    [Fact]
    public void IFileDiPartenzaSonoQuelliDelCampione()
    {
        var folder = Environment.GetEnvironmentVariable("PULSE_REFERENCE_FIXTURES");
        if (string.IsNullOrEmpty(folder)) return;
        using (var odt = File.Create(Path.Combine(folder, "references.odt"))) Assert.True(new OdtWriter().Write(_sample.Build(), odt).Succeeded);
        using (var docx = File.Create(Path.Combine(folder, "references.docx"))) Assert.True(_composer.Writer(DocxFlavour.Document).Write(_sample.Build(), docx).Succeeded);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void ICampiDelleProprietaAttraversanoIFormati(string route)
    {
        var paragraph = Paragraph(Route(route), "Titolo");
        Assert.Equal("Relazione", Assert.Single(Fields(paragraph, TextFieldKind.Title)).Text);
        Assert.Equal("Cliente", Assert.Single(Fields(paragraph, TextFieldKind.CustomProperty)).FieldArgument);
        Assert.Single(Fields(paragraph, TextFieldKind.WordCount));
        Assert.Equal(Docx(route) ? 0 : 1, Fields(paragraph, TextFieldKind.TableCount).Count());
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void LeVariabiliEIlTestoCondizionaleAttraversanoIFormati(string route)
    {
        var document = Route(route);
        var paragraph = document.Body.Blocks.OfType<ParagraphBlock>().First(block => block.Text.Contains(" letto ", StringComparison.Ordinal));
        var set = Assert.Single(Fields(paragraph, TextFieldKind.VariableSet));
        Assert.Equal("conto", set.FieldArgument);
        Assert.Equal("5", set.FieldDetail?.Format);
        Assert.Contains(Fields(paragraph, TextFieldKind.Variable), run => run.FieldArgument == "conto");
        var conditional = Assert.Single(Fields(paragraph, TextFieldKind.ConditionalText));
        Assert.Equal("cinque", conditional.FieldDetail?.WhenTrue);
        Assert.Equal("altro", conditional.FieldDetail?.WhenFalse);
        if (route is not ("docx-lo" or "docx-lo-odt"))
            Assert.True(new FieldCondition().Holds(conditional.FieldArgument, new Dictionary<string, string> { ["conto"] = "5" }));
        if (!Docx(route)) Assert.Equal("it", document.Variables["lingua"]);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void IlTestoNascostoCondizionaleVaInOdtESiDichiaraInDocx(string route)
    {
        var paragraph = Paragraph(Route(route), "Visibile");
        if (Docx(route))
        {
            Assert.Empty(Fields(paragraph, TextFieldKind.HiddenText));
            return;
        }
        var hidden = Assert.Single(Fields(paragraph, TextFieldKind.HiddenText));
        Assert.Equal("conto == 5", hidden.FieldArgument);
        Assert.True(hidden.Hidden);
    }

    [Fact]
    public void LePerditeDeiCampiSiDichiaranoInDocx()
    {
        var losses = _composer.Writer(DocxFlavour.Document).CheckFidelity(_sample.Build()).Losses.Select(loss => loss.Code).ToList();
        Assert.Contains("DOCX021", losses);
        Assert.Contains("DOCX022", losses);
        Assert.Contains("DOCX023", losses);
        Assert.DoesNotContain("DOCX025", losses);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void IRimandiAttraversanoIFormati(string route)
    {
        var document = Route(route);
        var paragraph = Paragraph(document, "Vedi");
        var references = Fields(paragraph, TextFieldKind.Bookmark).ToList();
        Assert.Contains(references, run => run.FieldArgument == "cap" && run.FieldDetail is null);
        Assert.Contains(references, run => run.FieldArgument == "cap" && run.FieldDetail?.Format == ReferenceFormats.Page);
        Assert.Contains(references, run => run.FieldArgument == "cap" && run.FieldDetail?.Format == ReferenceFormats.Direction);
        if (route != "docx-lo-odt")
        {
            var note = Assert.Single(Fields(paragraph, TextFieldKind.NoteReference));
            Assert.Equal(document.Notes.All.Single().Id, note.FieldArgument);
        }
        var caption = references.Single(run => run.FieldArgument != "cap");
        Assert.NotNull(new BookmarkCatalogProbe().Target(document, caption.FieldArgument!));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void IlCampoFissoResta(string route)
    {
        var date = Assert.Single(Fields(Paragraph(Route(route), "Data"), TextFieldKind.Date));
        Assert.True(date.FieldDetail?.Fixed);
        Assert.Equal(ThroughReferenceSuite(route) ? "01/01/00" : "01/01/2000", date.Text);
        if (!Docx(route)) Assert.Equal("2000-01-01", date.FieldDetail?.Value);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void LaCitazioneEIlSuoDatabaseAttraversanoIFormati(string route)
    {
        var document = Route(route);
        var citation = Assert.Single(Fields(Paragraph(document, "Come in"), TextFieldKind.Citation));
        Assert.Equal("Eco1977", citation.FieldArgument);
        var source = document.Bibliography.Find("Eco1977");
        if (route is "odt-lo-docx" or "docx-lo-odt") return;
        Assert.NotNull(source);
        Assert.Equal("Umberto Eco", source.Value("author"));
        Assert.Equal("Come si fa una tesi di laurea", source.Value("title"));
        Assert.Equal("1977", source.Value("year"));
        Assert.Equal("book", source.Type);
    }

    private IReadOnlyList<SectionBlock> Indexes(TextDocument document) =>
        [.. new BlockTree().Blocks(document.Body).OfType<SectionBlock>().Where(section => section.IsIndex)];

    [Theory]
    [MemberData(nameof(Routes))]
    public void IlSommarioAttraversaIFormati(string route)
    {
        var document = Route(route);
        var toc = Indexes(document).Single(section => section.Index!.Kind == IndexKind.Contents);
        Assert.Equal("Sommario", toc.Index!.Title);
        Assert.Equal(3, toc.Index.Levels);
        Assert.True(toc.Index.Hyperlinks);
        var lines = new BlockTree().Paragraphs(toc.Content).Select(paragraph => paragraph.Text).ToList();
        Assert.Equal("Sommario", lines[0]);
        Assert.StartsWith("Capitolo importante", lines[1], StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void GliAltriIndiciAttraversanoIFormati(string route)
    {
        var document = Route(route);
        var kinds = Indexes(document).Select(section => section.Index!).ToList();
        var alphabetical = kinds.Single(index => index.Kind == IndexKind.Alphabetical);
        Assert.True(alphabetical.LetterSeparators);
        var figures = kinds.Single(index => index.Kind == IndexKind.Illustrations);
        Assert.Equal(route is "docx-lo" or "odt-lo-docx" ? "Figura" : "Figure", figures.Sequence);
        var bibliography = kinds.Single(index => index.Kind == IndexKind.Bibliography);
        Assert.Equal(route == "docx-lo-odt" ? string.Empty : "Bibliografia", bibliography.Title);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void LeVociDellIndiceAnaliticoAttraversanoIFormati(string route)
    {
        var caption = Paragraph(Route(route), "Figura");
        Assert.Contains(caption.Marks, mark => mark is PulseOffice.Documents.Marks.IndexEntryMark { Entry: "grafico" });
    }

    private sealed class BookmarkCatalogProbe
    {
        public BookmarkEntry? Target(TextDocument document, string name) =>
            new BookmarkCatalog(new PulseOffice.Documents.Editing.StoryCatalog(new BlockTree())).Find(document, name);
    }
}

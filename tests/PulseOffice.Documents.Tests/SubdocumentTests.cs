using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class SubdocumentTests
{
    private sealed class FixedAuthor : IRevisionAuthor
    {
        public RevisionSignature Sign() => new("Anna", new DateTimeOffset(2026, 11, 6, 9, 0, 0, TimeSpan.Zero));
    }

    private sealed class Scene
    {
        public Scene()
        {
            var services = new ServiceCollection().AddDocumentsModel().AddSingleton<IRevisionAuthor>(new FixedAuthor()).BuildServiceProvider();
            Editor = services.GetRequiredService<IDocumentEditor>();
            Stories = services.GetRequiredService<IStoryCatalog>();
            Tree = services.GetRequiredService<IBlockTree>();
            Factory = services.GetRequiredService<ITextDocumentFactory>();
            Subdocuments = services.GetRequiredService<ISubdocuments>();
            Resolver = services.GetRequiredService<IRevisionResolver>();
            Document = Factory.Create();
        }

        public IDocumentEditor Editor { get; }

        public IStoryCatalog Stories { get; }

        public IBlockTree Tree { get; }

        public ITextDocumentFactory Factory { get; }

        public ISubdocuments Subdocuments { get; }

        public IRevisionResolver Resolver { get; }

        public TextDocument Document { get; }

        public UndoStack History { get; } = new();

        public ParagraphBlock First => (ParagraphBlock)Document.Body[0];

        public void Type(params string[] paragraphs) =>
            Editor.InsertText(Document, History, new DocumentPosition(First.Id, 0), string.Join("\n", paragraphs));
    }

    private readonly Scene _f = new();

    private ISubdocuments _subdocuments => _f.Subdocuments;

    private TextDocument Chapter(params string[] lines)
    {
        var chapter = _f.Factory.Create();
        chapter.Body.Replace(lines.Select((line, index) => new ParagraphBlock(chapter.NextId("p"), line, index == 0 ? DefaultStyleNames.HeadingLevel(1) : DefaultStyleNames.Standard)));
        return chapter;
    }

    [Fact]
    public void IlSottodocumentoEntraComeSezioneCollegataConIdNuoviEUnSoloAnnullamento()
    {
        _f.Type("Master");

        _subdocuments.Insert(_f.Document, _f.History, _f.First.Id, Chapter("Capitolo 1", "Testo"), "file:///tmp/c1.odt", "c1");

        var section = Assert.IsType<SectionBlock>(_f.Document.Body[1]);
        Assert.Equal("file:///tmp/c1.odt", section.LinkedFile);
        Assert.Equal(["Capitolo 1", "Testo"], section.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
        var ids = _f.Stories.Stories(_f.Document).SelectMany(story => _f.Tree.Blocks(story)).Select(block => block.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Single(_subdocuments.Linked(_f.Document));

        _f.History.Undo();

        Assert.Single(_f.Document.Body.Blocks);
    }

    [Fact]
    public void AggiornareRileggeIlContenutoDalFileESiAnnulla()
    {
        _f.Type("Master");
        _subdocuments.Insert(_f.Document, _f.History, _f.First.Id, Chapter("Capitolo 1", "Vecchio"), "c1.odt", "c1");
        var section = (SectionBlock)_f.Document.Body[1];

        _subdocuments.Refresh(_f.Document, _f.History, section.Id, Chapter("Capitolo 1", "Nuovo", "Aggiunto"));

        Assert.Equal(["Capitolo 1", "Nuovo", "Aggiunto"], section.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));

        _f.History.Undo();

        Assert.Equal(["Capitolo 1", "Vecchio"], section.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
    }

    [Fact]
    public void IlDocumentoInteroPortaStiliEOggetti()
    {
        var source = Chapter("Titolo");
        var fragment = _subdocuments.Whole(source);

        Assert.Single(fragment.Blocks.Blocks);
        Assert.Same(source.Styles, fragment.Styles);
    }

    private IEnumerable<string> Texts(SectionBlock section) => _f.Tree.Paragraphs(section.Content).Select(paragraph => paragraph.Text);

    private TextDocument MasterWithChapter(out SectionBlock section)
    {
        _f.Type("Master");
        _subdocuments.Insert(_f.Document, _f.History, _f.First.Id, Chapter("Capitolo 1", "Vecchio"), "c1.odt", "c1");
        section = (SectionBlock)_f.Document.Body[1];
        _f.Document.Changes.IsRecording = true;
        return _f.Document;
    }

    [Fact]
    public void ConLeRevisioniIlSottodocumentoInseritoEUnaRevisioneDiInserimento()
    {
        _f.Type("Master");
        _f.Document.Changes.IsRecording = true;

        _subdocuments.Insert(_f.Document, _f.History, _f.First.Id, Chapter("Capitolo 1", "Testo"), "c1.odt", "c1");

        Assert.IsType<SectionBlock>(_f.Document.Body[1]);
        var insertion = Assert.Single(_f.Document.Changes.All);
        Assert.IsType<Insertion>(insertion);
        Assert.Equal("Anna", insertion.Author);
    }

    [Fact]
    public void RifiutareIlSottodocumentoInseritoToglieLaSezioneIntera()
    {
        _f.Type("Master");
        _f.Document.Changes.IsRecording = true;
        _subdocuments.Insert(_f.Document, _f.History, _f.First.Id, Chapter("Capitolo 1", "Testo"), "c1.odt", "c1");

        _f.Resolver.ResolveAll(_f.Document, _f.History, RevisionVerdict.Reject);

        var only = Assert.IsType<ParagraphBlock>(Assert.Single(_f.Document.Body.Blocks));
        Assert.Equal("Master", only.Text);
        Assert.Empty(_f.Document.Changes.All);
        Assert.Empty(_subdocuments.Linked(_f.Document));
    }

    [Fact]
    public void AccettareIlSottodocumentoInseritoLasciaLaSezioneSenzaRevisioni()
    {
        _f.Type("Master");
        _f.Document.Changes.IsRecording = true;
        _subdocuments.Insert(_f.Document, _f.History, _f.First.Id, Chapter("Capitolo 1", "Testo"), "c1.odt", "c1");

        _f.Resolver.ResolveAll(_f.Document, _f.History, RevisionVerdict.Accept);

        var section = Assert.IsType<SectionBlock>(_f.Document.Body[1]);
        Assert.Equal(["Capitolo 1", "Testo"], Texts(section));
        Assert.Equal("Master", _f.First.Text);
        Assert.Empty(_f.Document.Changes.All);
    }

    [Fact]
    public void ConLeRevisioniAggiornareUnCollegamentoRegistraCancellazioneEInserimento()
    {
        MasterWithChapter(out var section);

        _subdocuments.Refresh(_f.Document, _f.History, section.Id, Chapter("Capitolo 1", "Nuovo", "Aggiunto"));

        Assert.Equal(["Capitolo 1", "Nuovo", "Aggiunto"], Texts(section));
        var deletion = Assert.Single(_f.Document.Changes.All.OfType<Deletion>());
        Assert.Equal(["Capitolo 1", "Vecchio"], _f.Tree.Paragraphs(deletion.Removed).Select(paragraph => paragraph.Text));
        Assert.Single(_f.Document.Changes.All.OfType<Insertion>());
    }

    [Fact]
    public void AccettareLAggiornamentoTieneSoloIlContenutoNuovoConISuoiStili()
    {
        MasterWithChapter(out var section);
        _subdocuments.Refresh(_f.Document, _f.History, section.Id, Chapter("Capitolo 1", "Nuovo", "Aggiunto"));

        _f.Resolver.ResolveAll(_f.Document, _f.History, RevisionVerdict.Accept);

        Assert.Equal(["Capitolo 1", "Nuovo", "Aggiunto"], Texts(section));
        var paragraphs = _f.Tree.Paragraphs(section.Content).ToList();
        Assert.Equal(DefaultStyleNames.HeadingLevel(1), paragraphs[0].StyleName);
        Assert.Equal(DefaultStyleNames.Standard, paragraphs[1].StyleName);
        Assert.Empty(_f.Document.Changes.All);
    }

    [Fact]
    public void RifiutareLAggiornamentoRidaIlContenutoVecchio()
    {
        MasterWithChapter(out var section);
        _subdocuments.Refresh(_f.Document, _f.History, section.Id, Chapter("Titolo nuovo", "Nuovo", "Aggiunto"));

        _f.Resolver.ResolveAll(_f.Document, _f.History, RevisionVerdict.Reject);

        Assert.Equal(["Capitolo 1", "Vecchio"], Texts(section));
        var paragraphs = _f.Tree.Paragraphs(section.Content).ToList();
        Assert.Equal(DefaultStyleNames.HeadingLevel(1), paragraphs[0].StyleName);
        Assert.Equal(DefaultStyleNames.Standard, paragraphs[1].StyleName);
        Assert.Empty(_f.Document.Changes.All);
    }

    [Fact]
    public void LAggiornamentoRegistratoEUnSoloPassoDiAnnullamento()
    {
        MasterWithChapter(out var section);
        var before = _f.History.StatePoint;

        _subdocuments.Refresh(_f.Document, _f.History, section.Id, Chapter("Capitolo 1", "Nuovo"));
        _f.History.Undo();

        Assert.Same(before, _f.History.StatePoint);
        Assert.Equal(["Capitolo 1", "Vecchio"], Texts(section));
        Assert.Empty(_f.Document.Changes.All);
    }
}

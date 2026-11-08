using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Revisions;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class TrackedPasteTests
{
    private sealed class FixedAuthor : IRevisionAuthor
    {
        public RevisionSignature Sign() => new("Anna", new DateTimeOffset(2026, 11, 6, 9, 0, 0, TimeSpan.Zero));
    }

    private readonly IDocumentEditor _editor;
    private readonly IDocumentClipboard _clipboard;
    private readonly IRevisionResolver _resolver;
    private readonly TextDocument _document;
    private readonly UndoStack _history = new();

    public TrackedPasteTests()
    {
        var services = new ServiceCollection().AddDocumentsModel().AddSingleton<IRevisionAuthor>(new FixedAuthor()).BuildServiceProvider();
        _editor = services.GetRequiredService<IDocumentEditor>();
        _clipboard = services.GetRequiredService<IDocumentClipboard>();
        _resolver = services.GetRequiredService<IRevisionResolver>();
        _document = services.GetRequiredService<ITextDocumentFactory>().Create();
        _editor.InsertText(_document, _history, new DocumentPosition(First.Id, 0), "inizio fine\nsorgente uno\nsorgente due");
    }

    private ParagraphBlock First => (ParagraphBlock)_document.Body[0];

    private DocumentFragment Source() =>
        _clipboard.Copy(_document, new DocumentRange(new DocumentPosition(((ParagraphBlock)_document.Body[1]).Id, 0), new DocumentPosition(((ParagraphBlock)_document.Body[2]).Id, 12)));

    private IEnumerable<string> Texts() => _document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text);

    [Fact]
    public void ConLeRevisioniIncollareRegistraUnInserimento()
    {
        var fragment = Source();
        _document.Changes.IsRecording = true;

        _clipboard.Paste(_document, _history, new DocumentPosition(First.Id, 7), fragment);

        Assert.Equal(["inizio sorgente uno", "sorgente duefine", "sorgente uno", "sorgente due"], Texts());
        Assert.IsType<Insertion>(Assert.Single(_document.Changes.All));
    }

    [Fact]
    public void RifiutareLIncollaturaRidaIlTestoDiPrima()
    {
        var fragment = Source();
        _document.Changes.IsRecording = true;
        _clipboard.Paste(_document, _history, new DocumentPosition(First.Id, 7), fragment);

        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);

        Assert.Equal(["inizio fine", "sorgente uno", "sorgente due"], Texts());
        Assert.Empty(_document.Changes.All);
    }

    [Fact]
    public void SenzaRevisioniIncollareNonRegistraNulla()
    {
        _clipboard.Paste(_document, _history, new DocumentPosition(First.Id, 7), Source());

        Assert.Empty(_document.Changes.All);
    }

    private DocumentFragment TrackedSource()
    {
        _document.Changes.IsRecording = true;
        _editor.InsertText(_document, _history, new DocumentPosition(((ParagraphBlock)_document.Body[1]).Id, 9), "nuovo ");
        _document.Changes.IsRecording = false;
        return _clipboard.Copy(_document, new DocumentRange(new DocumentPosition(((ParagraphBlock)_document.Body[1]).Id, 0), new DocumentPosition(((ParagraphBlock)_document.Body[1]).Id, 12)));
    }

    [Fact]
    public void SenzaRevisioniIncollareConservaLeRevisioniCopiateConIdentificativiPropri()
    {
        var fragment = TrackedSource();

        _clipboard.Paste(_document, _history, new DocumentPosition(First.Id, 7), fragment);

        var insertions = _document.Changes.All.OfType<Insertion>().ToList();
        Assert.Equal(2, insertions.Count);
        Assert.Equal(2, insertions.Select(change => change.Id).Distinct().Count());
        Assert.All(insertions, change => Assert.Equal("Anna", change.Author));
        _history.Undo();
        Assert.Single(_document.Changes.All);
    }

    [Fact]
    public void ConLeRevisioniIncollareTestoGiaRevisionatoLoRendeUnSoloInserimentoDiChiIncolla()
    {
        var fragment = TrackedSource();
        _document.Changes.IsRecording = true;

        _clipboard.Paste(_document, _history, new DocumentPosition(First.Id, 7), fragment);

        Assert.Equal(2, _document.Changes.All.Count);
        var marked = _document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks).OfType<PulseOffice.Documents.Marks.ChangeMark>().Select(mark => mark.ChangeId).Distinct();
        Assert.All(marked, id => Assert.NotNull(_document.Changes.Find(id)));
    }
}

using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class RevisionTests
{
    private sealed class FixedAuthor : IRevisionAuthor
    {
        public string Name { get; set; } = "Anna";

        public RevisionSignature Sign() => new(Name, new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero));
    }

    private readonly FixedAuthor _author = new();
    private readonly IDocumentEditor _editor;
    private readonly IRevisionResolver _resolver;
    private readonly IChangeRegions _regions;
    private readonly IDocumentComparison _comparison;
    private readonly TextDocument _document;
    private readonly UndoStack _history = new();

    public RevisionTests()
    {
        var services = new ServiceCollection().AddDocumentsModel().AddSingleton<IRevisionAuthor>(_author).BuildServiceProvider();
        _editor = services.GetRequiredService<IDocumentEditor>();
        _resolver = services.GetRequiredService<IRevisionResolver>();
        _regions = services.GetRequiredService<IChangeRegions>();
        _comparison = services.GetRequiredService<IDocumentComparison>();
        _document = services.GetRequiredService<ITextDocumentFactory>().Create();
    }

    private ParagraphBlock First => (ParagraphBlock)_document.Body[0];

    private DocumentPosition At(int offset) => new(First.Id, offset);

    private void Write(string text) => _editor.InsertText(_document, _history, At(First.Length), text);

    [Fact]
    public void ScrivereRegistrandoCreaUnaSolaInserzioneChePoiSiAllunga()
    {
        Write("Ciao ");
        _document.Changes.IsRecording = true;
        Write("mon");
        Write("do");

        var insertion = Assert.Single(_document.Changes.All);
        Assert.IsType<Insertion>(insertion);
        var region = _regions.Map(_document).Find(insertion.Id)!;
        Assert.Equal(5, region.Start.Offset);
        Assert.Equal(10, region.End.Offset);
        Assert.Equal("Anna", insertion.Author);
    }

    [Fact]
    public void UnAltroAutoreNonAllungaLInserzioneAltrui()
    {
        _document.Changes.IsRecording = true;
        Write("Anna");
        _author.Name = "Bruno";
        Write("Bruno");

        var regions = _regions.Map(_document).Regions;
        Assert.Equal(2, regions.Count);
        Assert.Equal((0, 4), (regions[0].Start.Offset, regions[0].End.Offset));
        Assert.Equal((4, 9), (regions[1].Start.Offset, regions[1].End.Offset));
        Assert.Equal("Bruno", regions[1].Change.Author);
    }

    [Fact]
    public void CancellareRegistrandoTieneIlTestoNellaRevisioneERifiutareLoRimette()
    {
        Write("Uno due tre");
        _document.Changes.IsRecording = true;
        _editor.Delete(_document, _history, DocumentRange.In(First.Id, 3, 7));

        Assert.Equal("Uno tre", First.Text);
        var deletion = Assert.IsType<Deletion>(Assert.Single(_document.Changes.All));
        Assert.Equal(" due", ((ParagraphBlock)deletion.Removed[0]).Text);

        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);
        Assert.Equal("Uno due tre", First.Text);
        Assert.Empty(_document.Changes.All);
        Assert.Empty(First.Marks.OfType<ChangeMark>());

        _history.Undo();
        Assert.Equal("Uno tre", First.Text);
        Assert.Single(_document.Changes.All);
    }

    [Fact]
    public void CancellareAllIndietroUnisceLeCancellazioniDelloStessoAutore()
    {
        Write("abcdef");
        _document.Changes.IsRecording = true;
        _editor.Delete(_document, _history, DocumentRange.In(First.Id, 5, 6));
        _editor.Delete(_document, _history, DocumentRange.In(First.Id, 4, 5));
        _editor.Delete(_document, _history, DocumentRange.In(First.Id, 3, 4));

        var deletion = Assert.IsType<Deletion>(Assert.Single(_document.Changes.All));
        Assert.Equal("def", ((ParagraphBlock)deletion.Removed[0]).Text);
        Assert.Equal("abc", First.Text);
    }

    [Fact]
    public void CancellareTestoAppenaInseritoLoTogliDavvero()
    {
        _document.Changes.IsRecording = true;
        Write("Helo");
        _editor.Delete(_document, _history, DocumentRange.In(First.Id, 3, 4));
        Write("lo");

        Assert.Equal("Hello", First.Text);
        Assert.IsType<Insertion>(Assert.Single(_document.Changes.All));
    }

    [Fact]
    public void CancellareTuttaLInserzioneLaToglieDalRegistro()
    {
        Write("x");
        _document.Changes.IsRecording = true;
        Write("ab");
        _editor.Delete(_document, _history, DocumentRange.In(First.Id, 1, 3));

        Assert.Equal("x", First.Text);
        Assert.Empty(_document.Changes.All);
        Assert.Empty(First.Marks);
    }

    [Fact]
    public void AccettareUnInserimentoTieneIlTestoERifiutarloLoToglie()
    {
        Write("A");
        _document.Changes.IsRecording = true;
        Write("B");
        Write("\nC");

        Assert.Equal(2, _document.Body.Count);
        var id = Assert.Single(_document.Changes.All).Id;
        _resolver.Resolve(_document, _history, [id], RevisionVerdict.Reject);
        Assert.Equal(1, _document.Body.Count);
        Assert.Equal("A", First.Text);
        _history.Undo();
        _resolver.Resolve(_document, _history, [id], RevisionVerdict.Accept);
        Assert.Equal(2, _document.Body.Count);
        Assert.Empty(_document.Changes.All);
        Assert.All(_document.Body.Blocks.Cast<ParagraphBlock>(), paragraph => Assert.Empty(paragraph.Marks));
    }

    [Fact]
    public void IlGrassettoRegistratoSiRifiutaTornandoAlTestoNormale()
    {
        Write("parola");
        _document.Changes.IsRecording = true;
        _editor.SetCharacter(_document, _history, DocumentRange.In(First.Id, 0, 6), new CharacterProperties { Bold = true });

        var change = Assert.IsType<FormatChange>(Assert.Single(_document.Changes.All));
        Assert.True(change.PreviousKnown);
        Assert.True(First.Content.Runs[0].Bold);

        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);
        Assert.False(First.Content.Runs[0].Bold);
        Assert.Empty(_document.Changes.All);
    }

    [Fact]
    public void LoStileDiParagrafoRegistratoSiRifiuta()
    {
        Write("Titolo");
        _document.Changes.IsRecording = true;
        _editor.ApplyParagraphStyle(_document, _history, DocumentRange.At(At(0)), "Heading 1");

        Assert.IsType<ParagraphFormatChange>(Assert.Single(_document.Changes.All));
        Assert.Equal("Heading 1", First.StyleName);
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);
        Assert.NotEqual("Heading 1", First.StyleName);
        Assert.Equal("Titolo", First.Text);
    }

    [Fact]
    public void InvioRegistratoSiRifiutaRiunendoIParagrafi()
    {
        Write("UnoDue");
        _document.Changes.IsRecording = true;
        _editor.SplitParagraph(_document, _history, At(3));

        Assert.Equal(2, _document.Body.Count);
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);
        Assert.Equal(1, _document.Body.Count);
        Assert.Equal("UnoDue", First.Text);
    }

    [Fact]
    public void CancellareUnaFineParagrafoRegistrataSiRifiuta()
    {
        Write("Uno\nDue");
        _document.Changes.IsRecording = true;
        _editor.MergeWithNext(_document, _history, First.Id);

        Assert.Equal(1, _document.Body.Count);
        Assert.Equal("UnoDue", First.Text);
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);
        Assert.Equal(["Uno", "Due"], _document.Body.Blocks.Cast<ParagraphBlock>().Select(paragraph => paragraph.Text));
    }

    [Fact]
    public void AccettarePerSelezioneTieneSoloLeRevisioniToccate()
    {
        _document.Changes.IsRecording = true;
        Write("uno");
        _author.Name = "Bruno";
        Write(" due");

        var resolved = _resolver.ResolveIn(_document, _history, DocumentRange.In(First.Id, 5, 6), RevisionVerdict.Accept);
        Assert.Equal(1, resolved);
        Assert.Equal("Anna", Assert.Single(_document.Changes.All).Author);
    }

    [Fact]
    public void ConfrontareSegnaParoleInseriteECancellate()
    {
        Write("Il gatto nero dorme\nSecondo paragrafo\nTerzo");
        var original = _document.Clone();
        var second = (ParagraphBlock)_document.Body[1];
        _editor.Delete(_document, _history, DocumentRange.In(First.Id, 9, 14));
        _editor.InsertText(_document, _history, At(9), "bianco ");
        _editor.MergeWithNext(_document, _history, second.Id);

        var added = _comparison.Compare(_document, original, _history, _author.Sign());

        Assert.True(added >= 2);
        Assert.Contains(_document.Changes.All, change => change is Deletion deletion && deletion.Removed.Blocks.OfType<ParagraphBlock>().Any(p => p.Text.Contains("nero", StringComparison.Ordinal)));
        Assert.Contains(_document.Changes.All, change => change is Insertion);
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);
        Assert.Equal(original.Body.Blocks.Cast<ParagraphBlock>().Select(p => p.Text), _document.Body.Blocks.Cast<ParagraphBlock>().Select(p => p.Text));
    }

    [Fact]
    public void UnireApplicaLeDifferenzeComeRevisioniDellAltroAutore()
    {
        Write("Primo\nSecondo");
        var other = _document.Clone();
        var copy = (ParagraphBlock)other.Body[1];
        copy.State = copy.State with { Content = new TextParagraph("Secondo riveduto") { Runs = [new TextRun("Secondo riveduto")] } };

        var added = _comparison.Merge(_document, other, _history, new RevisionSignature("Carla", DateTimeOffset.Now));

        Assert.True(added >= 1);
        Assert.Equal("Secondo riveduto", ((ParagraphBlock)_document.Body[1]).Text);
        Assert.All(_document.Changes.All, change => Assert.Equal("Carla", change.Author));
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);
        Assert.Equal("Secondo", ((ParagraphBlock)_document.Body[1]).Text);
    }

    [Fact]
    public void AnnullareUnaScritturaRegistrataTogliePureLaRevisione()
    {
        _document.Changes.IsRecording = true;
        using (_history.BeginTransaction("typing")) Write("abc");
        _history.Undo();
        Assert.Empty(_document.Changes.All);
        Assert.Equal(string.Empty, First.Text);
        _history.Redo();
        Assert.Single(_document.Changes.All);
    }

    [Fact]
    public void RifiutareRegistrandoTrasformaLInserimentoInCancellazioneELaCancellazioneInInserimento()
    {
        Write("Uno due");
        _document.Changes.IsRecording = true;
        _author.Name = "Bruno";
        Write(" tre");
        _editor.Delete(_document, _history, DocumentRange.In(First.Id, 0, 4));
        Assert.Equal("due tre", First.Text);
        _author.Name = "Anna";

        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reinstate);

        Assert.Equal("Uno due", First.Text);
        var changes = _regions.Map(_document).Regions;
        Assert.Contains(changes, region => region.Change is Insertion && region.Change.Author == "Anna" && region.Start.Offset == 0 && region.End.Offset == 4);
        Assert.Contains(changes, region => region.Change is Deletion { Author: "Anna" } deletion && ((ParagraphBlock)deletion.Removed[0]).Text == " tre");
        Assert.DoesNotContain(_document.Changes.All, change => change.Author == "Bruno");
    }
}

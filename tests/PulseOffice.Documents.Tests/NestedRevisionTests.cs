using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Revisions;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class NestedRevisionTests
{
    private sealed class SwitchableAuthor : IRevisionAuthor
    {
        public string Name { get; set; } = "Anna";

        public RevisionSignature Sign() => new(Name, new DateTimeOffset(2026, 11, 5, 9, 0, 0, TimeSpan.Zero));
    }

    private readonly SwitchableAuthor _author = new();
    private readonly IDocumentEditor _editor;
    private readonly IRevisionResolver _resolver;
    private readonly IChangeRegions _regions;
    private readonly IMetadataRedaction _redaction;
    private readonly TextDocument _document;
    private readonly UndoStack _history = new();

    public NestedRevisionTests()
    {
        var services = new ServiceCollection().AddDocumentsModel().AddSingleton<IRevisionAuthor>(_author).BuildServiceProvider();
        _editor = services.GetRequiredService<IDocumentEditor>();
        _resolver = services.GetRequiredService<IRevisionResolver>();
        _regions = services.GetRequiredService<IChangeRegions>();
        _redaction = services.GetRequiredService<IMetadataRedaction>();
        _document = services.GetRequiredService<ITextDocumentFactory>().Create();
    }

    private ParagraphBlock First => (ParagraphBlock)_document.Body[0];

    private Insertion AnnaInserts(string prefix, string inserted)
    {
        _editor.InsertText(_document, _history, new DocumentPosition(First.Id, 0), prefix);
        _document.Changes.IsRecording = true;
        _editor.InsertText(_document, _history, new DocumentPosition(First.Id, First.Length), inserted);
        _author.Name = "Bruno";
        return Assert.IsType<Insertion>(Assert.Single(_document.Changes.All));
    }

    private void Delete(int from, int to) => _editor.Delete(_document, _history, DocumentRange.In(First.Id, from, to));

    private Deletion Nested => Assert.Single(_document.Changes.All.OfType<Deletion>());

    [Fact]
    public void DeletingAnotherAuthorsInsertionKeepsTheTextAsADeletionNestedInIt()
    {
        var insertion = AnnaInserts("Uno ", "due tre");
        Delete(4, 8);

        Assert.Equal("Uno tre", First.Text);
        var deletion = Nested;
        Assert.Equal("Bruno", deletion.Author);
        Assert.Equal("due ", ((ParagraphBlock)deletion.Removed[0]).Text);
        Assert.Equal(insertion.Id, deletion.WithinInsertion);
        var region = _regions.Map(_document).Find(insertion.Id)!;
        Assert.Equal((4, 7), (region.Start.Offset, region.End.Offset));
    }

    [Fact]
    public void DeletingOnesOwnInsertionStillRemovesItForGood()
    {
        AnnaInserts("Uno ", "due tre");
        _author.Name = "Anna";
        Delete(4, 8);

        Assert.Equal("Uno tre", First.Text);
        Assert.IsType<Insertion>(Assert.Single(_document.Changes.All));
    }

    [Fact]
    public void DeletingAllOfAnotherAuthorsInsertionKeepsTheInsertionAlive()
    {
        var insertion = AnnaInserts("Uno ", "due");
        Delete(4, 7);

        Assert.Equal("Uno ", First.Text);
        Assert.NotNull(_document.Changes.Find(insertion.Id));
        Assert.Equal(insertion.Id, Nested.WithinInsertion);
    }

    [Fact]
    public void BackspacingThroughAnotherAuthorsInsertionGrowsOneNestedDeletion()
    {
        var insertion = AnnaInserts("Uno ", "abc");
        Delete(6, 7);
        Delete(5, 6);
        Delete(4, 5);

        Assert.Equal("Uno ", First.Text);
        Assert.Equal("abc", ((ParagraphBlock)Nested.Removed[0]).Text);
        Assert.Equal(insertion.Id, Nested.WithinInsertion);
    }

    [Fact]
    public void TheNestedDeletionIsOneUndoStep()
    {
        AnnaInserts("Uno ", "due tre");
        Delete(4, 8);
        _history.Undo();

        Assert.Equal("Uno due tre", First.Text);
        Assert.IsType<Insertion>(Assert.Single(_document.Changes.All));
    }

    [Fact]
    public void AcceptingTheNestedDeletionRemovesTheTextAndLeavesTheInsertion()
    {
        var insertion = AnnaInserts("Uno ", "due tre");
        Delete(4, 8);
        _resolver.Resolve(_document, _history, [Nested.Id], RevisionVerdict.Accept);

        Assert.Equal("Uno tre", First.Text);
        Assert.Equal(insertion.Id, Assert.IsType<Insertion>(Assert.Single(_document.Changes.All)).Id);
    }

    [Fact]
    public void AcceptingTheNestedDeletionOfAWholeInsertionLeavesNoChange()
    {
        AnnaInserts("Uno ", "due");
        Delete(4, 7);
        _resolver.Resolve(_document, _history, [Nested.Id], RevisionVerdict.Accept);

        Assert.Equal("Uno ", First.Text);
        Assert.Empty(_document.Changes.All);
    }

    [Fact]
    public void RejectingTheInsertionRemovesEverything()
    {
        var insertion = AnnaInserts("Uno ", "due tre");
        Delete(4, 8);
        _resolver.Resolve(_document, _history, [insertion.Id], RevisionVerdict.Reject);

        Assert.Equal("Uno ", First.Text);
        Assert.Empty(_document.Changes.All);
    }

    [Fact]
    public void RejectingAWhollyDeletedInsertionRemovesEverything()
    {
        var insertion = AnnaInserts("Uno ", "due");
        Delete(4, 7);
        _resolver.Resolve(_document, _history, [insertion.Id], RevisionVerdict.Reject);

        Assert.Equal("Uno ", First.Text);
        Assert.Empty(_document.Changes.All);
    }

    [Fact]
    public void RejectingTheDeletionLeavesTheWholeInsertion()
    {
        var insertion = AnnaInserts("Uno ", "due tre");
        Delete(4, 8);
        _resolver.Resolve(_document, _history, [Nested.Id], RevisionVerdict.Reject);

        Assert.Equal("Uno due tre", First.Text);
        Assert.IsType<Insertion>(Assert.Single(_document.Changes.All));
        var region = _regions.Map(_document).Find(insertion.Id)!;
        Assert.Equal((4, 11), (region.Start.Offset, region.End.Offset));
    }

    [Fact]
    public void RejectingTheDeletionOfAWholeInsertionGivesItBackToItsAuthor()
    {
        var insertion = AnnaInserts("Uno ", "due");
        Delete(4, 7);
        _resolver.Resolve(_document, _history, [Nested.Id], RevisionVerdict.Reject);

        Assert.Equal("Uno due", First.Text);
        var region = _regions.Map(_document).Find(insertion.Id)!;
        Assert.Equal("Anna", region.Change.Author);
        Assert.Equal((4, 7), (region.Start.Offset, region.End.Offset));
    }

    [Fact]
    public void AcceptingTheInsertionLeavesThePlainDeletionPending()
    {
        var insertion = AnnaInserts("Uno ", "due tre");
        Delete(4, 8);
        _resolver.Resolve(_document, _history, [insertion.Id], RevisionVerdict.Accept);

        Assert.Equal("Uno tre", First.Text);
        var deletion = Nested;
        Assert.Null(deletion.WithinInsertion);
        _resolver.Resolve(_document, _history, [deletion.Id], RevisionVerdict.Reject);
        Assert.Equal("Uno due tre", First.Text);
        Assert.Empty(_document.Changes.All);
    }

    [Theory]
    [InlineData(RevisionVerdict.Accept, "Uno tre")]
    [InlineData(RevisionVerdict.Reject, "Uno ")]
    public void ResolvingEverythingIsConsistentAndOneUndoStep(RevisionVerdict verdict, string expected)
    {
        AnnaInserts("Uno ", "due tre");
        Delete(4, 8);
        _resolver.ResolveAll(_document, _history, verdict);

        Assert.Equal(expected, First.Text);
        Assert.Empty(_document.Changes.All);
        _history.Undo();
        Assert.Equal("Uno tre", First.Text);
        Assert.Equal(2, _document.Changes.All.Count);
    }

    [Fact]
    public void AutoRedactionKeepsTheNestingAndMasksTheText()
    {
        var insertion = AnnaInserts("Uno ", "due tre");
        Delete(4, 8);
        _redaction.Redact(_document, _history, "Autore", text => new string('x', text.Length));

        Assert.All(_document.Changes.All, change => Assert.Equal("Autore", change.Author));
        Assert.Equal(insertion.Id, Nested.WithinInsertion);
        Assert.Equal("xxxx", ((ParagraphBlock)Nested.Removed[0]).Text);
    }

    [Fact]
    public void ADeletionThatPartlyCoversAnotherAuthorsInsertionIsSplitAtItsEdge()
    {
        var insertion = AnnaInserts("Uno ", "due tre");
        Delete(2, 8);

        Assert.Equal("Untre", First.Text);
        var deletions = _document.Changes.All.OfType<Deletion>().ToList();
        Assert.Equal(2, deletions.Count);
        var plain = Assert.Single(deletions, deletion => deletion.WithinInsertion is null);
        var nested = Assert.Single(deletions, deletion => deletion.WithinInsertion == insertion.Id);
        Assert.Equal("o ", ((ParagraphBlock)plain.Removed[0]).Text);
        Assert.Equal("due ", ((ParagraphBlock)nested.Removed[0]).Text);
        _history.Undo();
        Assert.Equal("Uno due tre", First.Text);
        Assert.Empty(_document.Changes.All.OfType<Deletion>());
    }

    [Fact]
    public void ADeletionThatPartlyCoversOnesOwnInsertionRemovesOnlyTheOwnPartForGood()
    {
        AnnaInserts("Uno ", "due tre");
        _author.Name = "Anna";
        Delete(2, 8);

        Assert.Equal("Untre", First.Text);
        var deletion = Assert.Single(_document.Changes.All.OfType<Deletion>());
        Assert.Null(deletion.WithinInsertion);
        Assert.Equal("o ", ((ParagraphBlock)deletion.Removed[0]).Text);
        Assert.Single(_document.Changes.All.OfType<Insertion>());
    }
}

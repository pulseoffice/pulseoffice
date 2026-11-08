using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Revisions;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class TrackedTextTransformTests
{
    private sealed class FixedAuthor : IRevisionAuthor
    {
        public RevisionSignature Sign() => new("Anna", new DateTimeOffset(2026, 11, 6, 9, 0, 0, TimeSpan.Zero));
    }

    private readonly IDocumentEditor _editor;
    private readonly IRevisionResolver _resolver;
    private readonly TextDocument _document;
    private readonly UndoStack _history = new();

    public TrackedTextTransformTests()
    {
        var services = new ServiceCollection().AddDocumentsModel().AddSingleton<IRevisionAuthor>(new FixedAuthor()).BuildServiceProvider();
        _editor = services.GetRequiredService<IDocumentEditor>();
        _resolver = services.GetRequiredService<IRevisionResolver>();
        _document = services.GetRequiredService<ITextDocumentFactory>().Create();
        _editor.InsertText(_document, _history, new DocumentPosition(First.Id, 0), "uno due tre");
        _document.Changes.IsRecording = true;
    }

    private ParagraphBlock First => (ParagraphBlock)_document.Body[0];

    [Fact]
    public void ATrackedCaseChangeIsADeletionAndAnInsertionNotAFormatChange()
    {
        _editor.TransformText(_document, _history, DocumentRange.In(First.Id, 4, 7), "case", text => text.ToUpperInvariant());

        Assert.Equal("uno DUE tre", First.Text);
        Assert.DoesNotContain(_document.Changes.All, change => change is FormatChange);
        var deletion = Assert.Single(_document.Changes.All.OfType<Deletion>());
        Assert.Equal("due", ((ParagraphBlock)deletion.Removed[0]).Text);
        Assert.Single(_document.Changes.All.OfType<Insertion>());
    }

    [Fact]
    public void RejectingATrackedCaseChangeGivesTheOriginalTextBack()
    {
        _editor.TransformText(_document, _history, DocumentRange.In(First.Id, 0, First.Length), "case", text => text.ToUpperInvariant());

        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);

        Assert.Equal("uno due tre", First.Text);
        Assert.Empty(_document.Changes.All);
    }

    [Fact]
    public void ATrackedCaseChangeIsOneUndoStep()
    {
        var before = _history.StatePoint;
        _editor.TransformText(_document, _history, DocumentRange.In(First.Id, 0, First.Length), "case", text => text.ToUpperInvariant());

        _history.Undo();
        Assert.Same(before, _history.StatePoint);
        Assert.Equal("uno due tre", First.Text);
        Assert.Empty(_document.Changes.All);
    }
}

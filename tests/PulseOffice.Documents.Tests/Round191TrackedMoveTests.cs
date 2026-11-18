using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Revisions;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round191TrackedMoveTests
{
    private readonly IRevisionResolver _resolver;
    private readonly TextDocument _document;
    private readonly UndoStack _history = new();

    public Round191TrackedMoveTests()
    {
        var services = new ServiceCollection().AddDocumentsModel().BuildServiceProvider();
        _resolver = services.GetRequiredService<IRevisionResolver>();
        _document = services.GetRequiredService<ITextDocumentFactory>().Create();
        var from = new Deletion("from", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("r1", new TextParagraph("moved"))])) { MoveId = "m1" };
        var to = new Insertion("to", "Ada", DateTimeOffset.UnixEpoch) { MoveId = "m1" };
        _document.Changes.Add(from);
        _document.Changes.Add(to);
        _document.Body.Replace(
        [
            new ParagraphBlock("p1", new TextParagraph("stays")) { Marks = [new ChangeMark(0, from.Id, MarkEdge.Point)] },
            new ParagraphBlock("p2", new TextParagraph("end moved")) { Marks = [new ChangeMark(4, to.Id, MarkEdge.Start), new ChangeMark(9, to.Id, MarkEdge.End)] },
        ]);
    }

    [Fact]
    public void Accepting_the_destination_settles_the_source_too()
    {
        _resolver.Resolve(_document, _history, ["to"], RevisionVerdict.Accept);

        Assert.Empty(_document.Changes.All);
        Assert.Equal(["stays", "end moved"], _document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
    }

    [Fact]
    public void Rejecting_the_source_puts_the_text_back_and_drops_the_destination()
    {
        _resolver.Resolve(_document, _history, ["from"], RevisionVerdict.Reject);

        Assert.Empty(_document.Changes.All);
        Assert.Equal(["movedstays", "end "], _document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
    }
}

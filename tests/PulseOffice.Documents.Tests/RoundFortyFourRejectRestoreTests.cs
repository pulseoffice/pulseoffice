using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Revisions;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class RoundFortyFourRejectRestoreTests
{
    private sealed class Author : IRevisionAuthor
    {
        public RevisionSignature Sign() => new("Anna", DateTimeOffset.UnixEpoch);
    }

    private readonly IDocumentEditor _editor;
    private readonly IRevisionResolver _resolver;
    private readonly IStoryCatalog _stories;
    private readonly ITableEditing _tables;
    private readonly IChangeRegions _regions;
    private readonly TextDocument _document;
    private readonly UndoStack _history = new();

    public RoundFortyFourRejectRestoreTests()
    {
        var services = new ServiceCollection().AddDocumentsModel().AddSingleton<IRevisionAuthor>(new Author()).BuildServiceProvider();
        _editor = services.GetRequiredService<IDocumentEditor>();
        _resolver = services.GetRequiredService<IRevisionResolver>();
        _stories = services.GetRequiredService<IStoryCatalog>();
        _tables = services.GetRequiredService<ITableEditing>();
        _regions = services.GetRequiredService<IChangeRegions>();
        _document = services.GetRequiredService<ITextDocumentFactory>().Create();
    }

    private ParagraphBlock Paragraph(int index) => (ParagraphBlock)_document.Body[index];

    private IReadOnlyList<InlineMark> AllMarks() => [.. _stories.Paragraphs(_document).SelectMany(paragraph => paragraph.Marks)];

    private void Type(string text) => _editor.InsertText(_document, _history, new DocumentPosition(Paragraph(0).Id, 0), text);

    private void DeleteMiddleTracked()
    {
        _document.Changes.IsRecording = true;
        _editor.Delete(_document, _history, new DocumentRange(new DocumentPosition(Paragraph(0).Id, 2), new DocumentPosition(Paragraph(2).Id, 1)));
        _document.Changes.IsRecording = false;
    }

    private AnchoredObject Frame(string id, string paragraphId) =>
        new(id, new ParagraphAnchor(paragraphId), new TextBoxContent(new BlockContainer([new ParagraphBlock(id + "-p", "riquadro")])),
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(2)));

    [Fact]
    public void RejectingATrackedDeletionBringsBackAFrameAnchoredToAWhollyDeletedParagraph()
    {
        Type("uno\ndue\ntre");
        _document.Objects.Add(Frame("frame1", Paragraph(1).Id));

        DeleteMiddleTracked();

        Assert.Single(_document.Objects.All);
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);

        Assert.Equal(["uno", "due", "tre"], _document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
        var frame = Assert.Single(_document.Objects.All);
        Assert.Equal(Paragraph(1).Id, frame.Anchor.Accept(new AnchorParagraph()));
    }

    [Fact]
    public void AcceptingATrackedDeletionRemovesTheFrameAndUndoBringsItBack()
    {
        Type("uno\ndue\ntre");
        _document.Objects.Add(Frame("frame1", Paragraph(1).Id));
        DeleteMiddleTracked();

        _resolver.ResolveAll(_document, _history, RevisionVerdict.Accept);

        Assert.Empty(_document.Objects.All);
        _history.Undo();
        Assert.Single(_document.Objects.All);
        _history.Undo();
        Assert.Equal(Paragraph(1).Id, Assert.Single(_document.Objects.All).Anchor.Accept(new AnchorParagraph()));
    }

    [Fact]
    public void RejectingATrackedDeletionBringsBackACommentWhoseRangeWasDeleted()
    {
        Type("uno\ndue\ntre");
        _editor.AddComment(_document, _history, DocumentRange.In(Paragraph(1).Id, 0, 2), "Bea", DateTimeOffset.UnixEpoch, "vedi");

        DeleteMiddleTracked();

        Assert.Single(_document.Comments.All);
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);

        var comment = Assert.Single(_document.Comments.All);
        var marks = Paragraph(1).Marks.OfType<CommentMark>().ToList();
        Assert.Equal(2, marks.Count);
        Assert.All(marks, mark => Assert.Equal(comment.Id, mark.CommentId));
        Assert.Equal(0, marks.Single(mark => mark.Edge == MarkEdge.Start).Offset);
        Assert.Equal(2, marks.Single(mark => mark.Edge == MarkEdge.End).Offset);
    }

    [Fact]
    public void AcceptingATrackedDeletionRemovesTheCommentWhoseRangeWasDeleted()
    {
        Type("uno\ndue\ntre");
        _editor.AddComment(_document, _history, DocumentRange.In(Paragraph(1).Id, 0, 2), "Bea", DateTimeOffset.UnixEpoch, "vedi");
        DeleteMiddleTracked();

        _resolver.ResolveAll(_document, _history, RevisionVerdict.Accept);

        Assert.Empty(_document.Comments.All);
        Assert.DoesNotContain(AllMarks(), mark => mark is CommentMark);
    }

    [Fact]
    public void RejectingATrackedDeletionOfHalfAFieldKeepsTheFieldWhole()
    {
        Type("uno\nab12\n34cd");
        Paragraph(1).Marks = [new FieldCodeMark(2, "field1", FieldCodePart.Begin, " PAGE "), new FieldCodeMark(2, "field1", FieldCodePart.Separate, string.Empty)];
        Paragraph(2).Marks = [new FieldCodeMark(2, "field1", FieldCodePart.End, string.Empty)];
        _document.Changes.IsRecording = true;
        _editor.Delete(_document, _history, new DocumentRange(new DocumentPosition(Paragraph(0).Id, 3), new DocumentPosition(Paragraph(2).Id, 0)));
        _document.Changes.IsRecording = false;

        Assert.Equal(3, AllMarks().OfType<FieldCodeMark>().Count());
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);

        var parts = AllMarks().OfType<FieldCodeMark>().ToList();
        Assert.Equal(3, parts.Count);
        Assert.Single(parts.Select(mark => mark.FieldId).Distinct());
        Assert.Equal(["uno", "ab12", "34cd"], _document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
    }

    [Fact]
    public void DeletingATableRowThatHoldsAnInsertionStartKeepsTheRestOfTheInsertionAsARegion()
    {
        Type("fine");
        _editor.InsertTable(_document, _history, new DocumentPosition(Paragraph(0).Id, 0), 2, 1);
        var table = _document.Body.Blocks.OfType<TableBlock>().Single();
        var cell = table.Cell(0, 0).Content.Blocks.OfType<ParagraphBlock>().First();
        var after = _document.Body.Blocks.OfType<ParagraphBlock>().Last();
        _document.Changes.Add(new Insertion("ct1", "Ada", DateTimeOffset.UnixEpoch));
        cell.Marks = [new ChangeMark(0, "ct1", MarkEdge.Start)];
        after.Marks = [new ChangeMark(2, "ct1", MarkEdge.End)];

        _tables.DeleteRow(_document, _history, _tables.LocateRow(_document, table, 0)!);

        var region = _regions.Map(_document).Find("ct1");
        Assert.NotNull(region);
        Assert.False(region.IsPoint);
        Assert.Equal(new DocumentPosition(after.Id, 2), region.End);
        Assert.Equal(new DocumentPosition(table.Cell(0, 0).Content.Blocks.OfType<ParagraphBlock>().First().Id, 0), region.Start);
    }

    [Fact]
    public void DeletingATableRowThatHoldsAnInsertionEndEndsTheInsertionBeforeTheRow()
    {
        Type("inizio");
        _editor.InsertTable(_document, _history, new DocumentPosition(Paragraph(0).Id, 6), 2, 1);
        var before = Paragraph(0);
        var table = _document.Body.Blocks.OfType<TableBlock>().Single();
        var cell = table.Cell(1, 0).Content.Blocks.OfType<ParagraphBlock>().First();
        _document.Changes.Add(new Insertion("ct1", "Ada", DateTimeOffset.UnixEpoch));
        before.Marks = [new ChangeMark(1, "ct1", MarkEdge.Start)];
        cell.Marks = [new ChangeMark(0, "ct1", MarkEdge.End)];
        var keptCell = table.Cell(0, 0).Content.Blocks.OfType<ParagraphBlock>().Last();

        _tables.DeleteRow(_document, _history, _tables.LocateRow(_document, table, 1)!);

        var region = _regions.Map(_document).Find("ct1");
        Assert.NotNull(region);
        Assert.Equal(new DocumentPosition(before.Id, 1), region.Start);
        Assert.Equal(new DocumentPosition(keptCell.Id, keptCell.Length), region.End);
        _history.Undo();
        Assert.Equal(new DocumentPosition(cell.Id, 0), _regions.Map(_document).Find("ct1")!.End);
    }
}

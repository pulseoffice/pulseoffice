using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Revisions;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class ParagraphMarkRevisionTests
{
    private readonly IRevisionResolver _resolver;
    private readonly TextDocument _document;
    private readonly UndoStack _history = new();

    public ParagraphMarkRevisionTests()
    {
        var services = new ServiceCollection().AddDocumentsModel().BuildServiceProvider();
        _resolver = services.GetRequiredService<IRevisionResolver>();
        _document = services.GetRequiredService<ITextDocumentFactory>().Create();
        var deletion = new Deletion("change1", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer(
        [
            new ParagraphBlock("r1", new TextParagraph("cd") { Align = TextAlign.Center }),
            new ParagraphBlock("r2", new TextParagraph("ef")),
        ]));
        _document.Changes.Add(deletion);
        _document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("abgh") { Align = TextAlign.Center }) { Marks = [new ChangeMark(2, deletion.Id, MarkEdge.Point)] }]);
    }

    [Fact]
    public void RejectingAJoinedParagraphMarkSplitsTheParagraphsAgainWithTheirOwnLayout()
    {
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);

        var paragraphs = _document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal(["abcd", "efgh"], paragraphs.Select(paragraph => paragraph.Text));
        Assert.Equal([TextAlign.Center, TextAlign.Left], paragraphs.Select(paragraph => paragraph.Content.Align));
        Assert.Empty(_document.Changes.All);
    }

    private TableBlock TrackedTable()
    {
        _document.Changes.Add(new Insertion("row1", "Ada", DateTimeOffset.UnixEpoch));
        _document.Changes.Add(new Deletion("row2", "Bea", DateTimeOffset.UnixEpoch, new BlockContainer()));
        TableRow Row(string text, string? change) => new([new TableCell(new BlockContainer([new ParagraphBlock(text, text)]))]) { ChangeId = change };
        var table = new TableBlock("t1", [Row("kept", null), Row("new", "row1"), Row("old", "row2")], [new Foundation.Units.Twip(1440)]);
        _document.Body.Add(table);
        return table;
    }

    private IEnumerable<string> RowTexts(TableBlock table) => table.Rows.Select(row => ((ParagraphBlock)row.Cells[0].Content[0]).Text);

    [Fact]
    public void AcceptingTrackedRowsKeepsTheInsertedRowAndDropsTheDeletedOne()
    {
        var table = TrackedTable();

        _resolver.Resolve(_document, _history, ["row1", "row2"], RevisionVerdict.Accept);

        Assert.Equal(["kept", "new"], RowTexts(table));
        Assert.All(table.Rows, row => Assert.Null(row.ChangeId));
        Assert.DoesNotContain(_document.Changes.All, change => change.Id is "row1" or "row2");
        _history.Undo();
        Assert.Equal(["kept", "new", "old"], RowTexts(table));
        Assert.Equal("row2", table.Rows[2].ChangeId);
    }

    [Fact]
    public void RejectingAnInsertedRowWhoseCellHoldsOnlyANestedTableDeletesTheRow()
    {
        _document.Changes.Add(new Insertion("row1", "Ada", DateTimeOffset.UnixEpoch));
        var nested = new TableBlock("t2", [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("inner", "inner")]))])], [new Foundation.Units.Twip(720)]);
        var table = new TableBlock("t1",
        [
            new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("kept", "kept")]))]),
            new TableRow([new TableCell(new BlockContainer([nested]))]) { ChangeId = "row1" },
        ], [new Foundation.Units.Twip(1440)]);
        _document.Body.Add(table);

        _resolver.Resolve(_document, _history, ["row1"], RevisionVerdict.Reject);

        Assert.Single(table.Rows);
        Assert.DoesNotContain(_document.Changes.All, change => change.Id == "row1");
        _history.Undo();
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("row1", table.Rows[1].ChangeId);
    }

    [Fact]
    public void RejectingTrackedRowsDropsTheInsertedRowAndKeepsTheDeletedOne()
    {
        var table = TrackedTable();

        _resolver.Resolve(_document, _history, ["row1", "row2"], RevisionVerdict.Reject);

        Assert.Equal(["kept", "old"], RowTexts(table));
        Assert.All(table.Rows, row => Assert.Null(row.ChangeId));
        Assert.DoesNotContain(_document.Changes.All, change => change.Id is "row1" or "row2");
    }

    [Fact]
    public void AcceptingAJoinedParagraphMarkKeepsOneParagraph()
    {
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Accept);

        Assert.Equal("abgh", Assert.Single(_document.Body.Blocks.OfType<ParagraphBlock>()).Text);
        Assert.Empty(_document.Changes.All);
        Assert.Empty(((ParagraphBlock)_document.Body[0]).Marks);
    }
}

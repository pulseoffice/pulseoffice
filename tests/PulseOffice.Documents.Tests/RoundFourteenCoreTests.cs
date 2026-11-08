using System.Diagnostics;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class RoundFourteenCoreTests
{
    private static Fixture LongDocument(int paragraphs, int commentEvery)
    {
        var f = new Fixture();
        f.Document.Body.Replace([]);
        for (var index = 0; index < paragraphs; index++)
        {
            var block = new ParagraphBlock(f.Document.NextId("p"), new TextParagraph("testo " + index) { Runs = [new TextRun("testo " + index)] });
            if (commentEvery > 0 && index % commentEvery == 0)
            {
                var id = "c" + index;
                f.Document.Comments.Add(new Comment(id, "a", DateTimeOffset.UnixEpoch, new BlockContainer()));
                block.State = block.State with { Marks = [new CommentMark(0, id, MarkEdge.Start), new CommentMark(3, id, MarkEdge.End)] };
            }
            f.Document.Body.Add(block);
        }
        return f;
    }

    [Fact]
    public void DeletingTheWholeOfALongDocumentTakesLinearTime()
    {
        var f = LongDocument(60_000, 0);
        var last = f.Paragraph(f.Document.Body.Count - 1);
        var watch = Stopwatch.StartNew();
        f.Editor.Delete(f.Document, f.History, new DocumentRange(new DocumentPosition(f.First.Id, 0), new DocumentPosition(last.Id, last.Length)));
        var elapsed = watch.Elapsed;
        Assert.Equal(1, f.Document.Body.Count);
        Assert.True(elapsed < TimeSpan.FromSeconds(2), $"deleted in {elapsed}");
    }

    [Fact]
    public void DeletingALongCommentedRangeTakesLinearTime()
    {
        var f = LongDocument(20_000, 4);
        var last = f.Paragraph(f.Document.Body.Count - 1);
        var watch = Stopwatch.StartNew();
        f.Editor.Delete(f.Document, f.History, new DocumentRange(new DocumentPosition(f.First.Id, 0), new DocumentPosition(last.Id, last.Length)));
        var elapsed = watch.Elapsed;
        Assert.Empty(f.Document.Comments.All);
        Assert.True(elapsed < TimeSpan.FromSeconds(2), $"deleted in {elapsed}");
    }

    private static (Fixture Fixture, TableBlock Table) TableWithNote()
    {
        var f = new Fixture();
        f.Editor.InsertTable(f.Document, f.History, new DocumentPosition(f.First.Id, 0), 3, 2);
        var table = f.Document.Body.Blocks.OfType<TableBlock>().Single();
        var cell = table.Cell(1, 0).Content.Blocks.OfType<ParagraphBlock>().First();
        f.Editor.InsertText(f.Document, f.History, new DocumentPosition(cell.Id, 0), "nota");
        f.Editor.InsertNote(f.Document, f.History, new DocumentPosition(cell.Id, 4), NoteKind.Footnote, "testo della nota");
        return (f, table);
    }

    [Fact]
    public void DeletingARowRemovesTheNotesOfItsCellsAndUndoBringsThemBack()
    {
        var (f, table) = TableWithNote();
        var tables = new TableEditing(f.Stories, f.Deletion);
        var cell = table.Cell(1, 0).Content.Blocks.OfType<ParagraphBlock>().First();
        var before = f.Snapshot();
        tables.DeleteRow(f.Document, f.History, tables.Locate(f.Document, cell.Id)!);
        Assert.Empty(f.Document.Notes.All);
        f.History.Undo();
        Assert.Equal(before, f.Snapshot());
        Assert.Single(f.Document.Notes.All);
    }

    [Fact]
    public void DeletingAColumnOrTheTableRemovesTheNotesOfItsCells()
    {
        var (f, table) = TableWithNote();
        var tables = new TableEditing(f.Stories, f.Deletion);
        var cell = table.Cell(1, 0).Content.Blocks.OfType<ParagraphBlock>().First();
        tables.DeleteColumn(f.Document, f.History, tables.Locate(f.Document, cell.Id)!);
        Assert.Empty(f.Document.Notes.All);
        f.History.Undo();
        Assert.Single(f.Document.Notes.All);
        tables.DeleteTable(f.Document, f.History, tables.Locate(f.Document, cell.Id)!);
        Assert.Empty(f.Document.Notes.All);
    }

    [Fact]
    public void DeletingAHeaderRowDoesNotTurnABodyRowIntoAHeader()
    {
        var (f, table) = TableWithNote();
        table.HeaderRows = 1;
        var tables = new TableEditing(f.Stories, f.Deletion);
        var header = table.Cell(0, 0).Content.Blocks.OfType<ParagraphBlock>().First();
        tables.DeleteRow(f.Document, f.History, tables.Locate(f.Document, header.Id)!);
        Assert.Equal(0, table.HeaderRows);
        f.History.Undo();
        Assert.Equal(1, table.HeaderRows);
        tables.InsertRow(f.Document, f.History, tables.Locate(f.Document, header.Id)!, below: false);
        Assert.Equal(2, table.HeaderRows);
    }

    [Fact]
    public void UndoingADeletionAcrossCommentsRestoresCommentsAndTheirMarks()
    {
        var f = LongDocument(12, 4);
        var before = f.Snapshot();
        var end = f.Paragraph(9);
        f.Editor.Delete(f.Document, f.History, new DocumentRange(new DocumentPosition(f.Paragraph(2).Id, 2), new DocumentPosition(end.Id, 1)));
        Assert.DoesNotContain(f.Document.Comments.All, comment => comment.Id == "c4");
        Assert.DoesNotContain(f.Document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks).OfType<CommentMark>(), mark => mark.CommentId == "c8");
        f.History.Undo();
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(2, f.Paragraph(4).Marks.Count);
    }
}

using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class RoundFortyThreeEditingTests
{
    private readonly Fixture _f = new();

    private IReadOnlyList<InlineMark> AllMarks() => [.. _f.Stories.Paragraphs(_f.Document).SelectMany(paragraph => paragraph.Marks)];

    [Fact]
    public void DeletingTheParagraphThatHoldsABookmarkStartMovesTheStartToTheDeletionPoint()
    {
        _f.Type("uno", "due", "tre", "fine");
        _f.Paragraph(1).Marks = [new BookmarkMark(1, "segno", MarkEdge.Start)];
        _f.Paragraph(2).Marks = [new BookmarkMark(2, "segno", MarkEdge.End)];
        var before = _f.Snapshot();

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 1), _f.At(2, 1)));

        var marks = AllMarks().OfType<BookmarkMark>().ToList();
        Assert.Equal(2, marks.Count);
        var start = Assert.Single(marks, mark => mark.Edge == MarkEdge.Start);
        var end = Assert.Single(marks, mark => mark.Edge == MarkEdge.End);
        Assert.Equal(1, start.Offset);
        Assert.Equal(2, end.Offset);
        Assert.Equal("ure", _f.First.Text);
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
    }

    [Fact]
    public void DeletingTheParagraphThatHoldsATrackedInsertionStartKeepsTheRestOfTheInsertionTracked()
    {
        _f.Type("uno", "due", "tre");
        _f.Document.Changes.Add(new Insertion("ct1", "Ada", DateTimeOffset.UnixEpoch));
        _f.Paragraph(1).Marks = [new ChangeMark(1, "ct1", MarkEdge.Start)];
        _f.Paragraph(2).Marks = [new ChangeMark(2, "ct1", MarkEdge.End)];

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 1), _f.At(2, 1)));

        var marks = AllMarks().OfType<ChangeMark>().ToList();
        Assert.Contains(marks, mark => mark.Edge == MarkEdge.Start && mark.Offset == 1);
        Assert.Contains(marks, mark => mark.Edge == MarkEdge.End && mark.Offset == 2);
    }

    [Fact]
    public void DeletingATableRowThatHoldsABookmarkStartDropsTheWholeBookmark()
    {
        _f.Editor.InsertTable(_f.Document, _f.History, new DocumentPosition(_f.First.Id, 0), 2, 1);
        var table = _f.Document.Body.Blocks.OfType<TableBlock>().Single();
        var cell = table.Cell(0, 0).Content.Blocks.OfType<ParagraphBlock>().First();
        cell.Marks = [new BookmarkMark(0, "segno", MarkEdge.Start)];
        var after = _f.Document.Body.Blocks.OfType<ParagraphBlock>().Last();
        after.Marks = [new BookmarkMark(0, "segno", MarkEdge.End)];
        var tables = new TableEditing(_f.Stories, _f.Deletion);

        tables.DeleteRow(_f.Document, _f.History, tables.Locate(_f.Document, cell.Id)!);

        Assert.DoesNotContain(AllMarks(), mark => mark is BookmarkMark);
        _f.History.Undo();
        Assert.Equal(2, AllMarks().OfType<BookmarkMark>().Count());
    }

    [Fact]
    public void SplittingAtACollapsedBookmarkKeepsItsStartBeforeItsEnd()
    {
        _f.Type("abcd");
        _f.First.Marks = [new BookmarkMark(2, "segno", MarkEdge.Start), new BookmarkMark(2, "segno", MarkEdge.End)];

        _f.Editor.SplitParagraph(_f.Document, _f.History, _f.At(0, 2));

        Assert.Equal(2, _f.First.Marks.OfType<BookmarkMark>().Count());
        Assert.Empty(_f.Paragraph(1).Marks);
    }

    [Fact]
    public void DeletingAcrossParagraphsAFieldThatSpansThemRemovesTheField()
    {
        _f.Type("uno ab", "cd fine");
        _f.Paragraph(0).Marks = [new FieldCodeMark(4, "field1", FieldCodePart.Begin, " DATE "), new FieldCodeMark(4, "field1", FieldCodePart.Separate, string.Empty)];
        _f.Paragraph(1).Marks = [new FieldCodeMark(2, "field1", FieldCodePart.End, string.Empty)];

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 3), _f.At(1, 2)));

        Assert.Equal("uno fine", _f.First.Text);
        Assert.DoesNotContain(AllMarks(), mark => mark is FieldCodeMark);
        _f.History.Undo();
        Assert.Equal(3, AllMarks().OfType<FieldCodeMark>().Count());
    }

    [Fact]
    public void DeletingARowUnderAVerticalMergeKeepsTheProtectionAndNumberFormatOfTheMergedCell()
    {
        _f.Editor.InsertTable(_f.Document, _f.History, new DocumentPosition(_f.First.Id, 0), 3, 1);
        var table = _f.Document.Body.Blocks.OfType<TableBlock>().Single();
        var origin = table.Cell(0, 0);
        origin.RowSpan = 2;
        origin.Protected = true;
        origin.NumberFormat = new CellNumberFormat(CellValueKind.Percent, 1);
        table.Cell(1, 0).Covered = true;
        var tables = new TableEditing(_f.Stories, _f.Deletion);

        tables.DeleteRow(_f.Document, _f.History, tables.LocateRow(_f.Document, table, 0)!);

        var heir = table.Cell(0, 0);
        Assert.True(heir.Protected);
        Assert.Equal(CellValueKind.Percent, heir.NumberFormat?.Kind);
    }
}

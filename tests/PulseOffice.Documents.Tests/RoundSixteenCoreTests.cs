using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Documents.Marks;
using Xunit;

namespace PulseOffice.Documents.Tests;

[Collection(DocumentTimingCollection.Name)]
public sealed class RoundSixteenCoreTests
{
    private const int Many = 30_000;

    private readonly Fixture _f = new();
    private readonly ContentControlCatalog _catalog;
    private readonly ContentControlEditing _editing;

    public RoundSixteenCoreTests()
    {
        _catalog = new ContentControlCatalog(_f.Stories, _f.Tree);
        _editing = new ContentControlEditing(_catalog, _f.Stories, _f.Text, _f.Deletion, _f.Trails, new MarkOrder(), new PulseOffice.Text.Rich.TextCultures());
    }

    [Fact]
    public void DeletingAParagraphFullOfCommentsTakesLinearTime()
    {
        var text = new string('x', Many * 2);
        var marks = Enumerable.Range(0, Many).SelectMany(index => new InlineMark[]
        {
            new CommentMark(index * 2, "c" + index, MarkEdge.Start),
            new CommentMark(index * 2 + 1, "c" + index, MarkEdge.End),
        }).ToList();
        var state = new ParagraphState(new PulseOffice.Text.Rich.TextParagraph(text), marks);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var after = new ParagraphText().Delete(state, 1, text.Length - 1);
        clock.Stop();
        Assert.Equal(2, after.Content.Text.Length);
        Assert.Equal(["c0"], after.Marks.OfType<CommentMark>().Where(mark => mark.Edge == MarkEdge.Start).Select(mark => mark.CommentId));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"deleted in {clock.Elapsed}");
    }

    [Fact]
    public void FillingAControlBesideManyMarksTakesLinearTime()
    {
        var marks = new List<InlineMark>
        {
            new ContentControlMark(2, "k", MarkEdge.Start) { Control = new ContentControl { Kind = ContentControlKind.PlainText } },
            new ContentControlMark(2, "k", MarkEdge.End),
        };
        marks.AddRange(Enumerable.Range(0, Many).Select(index => new CommentMark(2, "p" + index, MarkEdge.Point)));
        _f.Document.Body.Replace([new ParagraphBlock("a", "abcd") { Marks = marks }]);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _editing.Fill(_f.Document, _f.History, "k", "zz");
        clock.Stop();
        var paragraph = _f.Document.Body.Blocks.OfType<ParagraphBlock>().Single();
        Assert.Equal("abzzcd", paragraph.Text);
        Assert.All(paragraph.Marks.OfType<CommentMark>(), mark => Assert.Equal(4, mark.Offset));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"filled in {clock.Elapsed}");
    }

    [Fact]
    public void RefillingManyEmptyControlsWalksTheDocumentOnce()
    {
        const int count = 3_000;
        _f.Document.Body.Replace([.. Enumerable.Range(0, count).Select(index => new ParagraphBlock("p" + index, "ab")
        {
            Marks =
            [
                new ContentControlMark(1, "k" + index, MarkEdge.Start) { Control = new ContentControl { Kind = ContentControlKind.RichText } },
                new ContentControlMark(1, "k" + index, MarkEdge.End),
                new ContentControlMark(2, "j" + index, MarkEdge.Start) { Control = new ContentControl { Kind = ContentControlKind.RichText } },
                new ContentControlMark(2, "j" + index, MarkEdge.End),
            ],
        })]);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _editing.Refill(_f.Document, _f.History, "…");
        clock.Stop();
        Assert.All(_f.Document.Body.Blocks.OfType<ParagraphBlock>(), paragraph => Assert.Equal("a…b…", paragraph.Text));
        Assert.All(_catalog.All(_f.Document), span => Assert.True(span.Control.ShowingPlaceholder));
        Assert.Equal("…", _f.Document.Body.Blocks.OfType<ParagraphBlock>().First().Text[3..4]);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"refilled in {clock.Elapsed}");
    }

    private TableBlock Grid(string id, params string[] rows) =>
        new(id, rows.Select((text, index) => new TableRow([new TableCell(new BlockContainer([new ParagraphBlock(id + "r" + index, text)]))])), [new PulseOffice.Foundation.Units.Twip(1440)]);

    private TableConversion Conversion() => new(_f.Stories, _f.Text, new TableCellFactory(), _f.Deletion, new TableSpanCutting());

    [Fact]
    public void SplittingATableInsideItsHeadingKeepsTheHeadingCountsWithinEachTable()
    {
        var table = Grid("t", "h1", "h2", "h3", "a", "b");
        table.HeaderRows = 3;
        _f.Document.Body.Replace([table]);
        var at = new TableEditing(_f.Stories, _f.Deletion).Locate(_f.Document, "tr1")!;

        Conversion().SplitTable(_f.Document, _f.History, at, copyHeading: true);

        var tables = _f.Document.Body.Blocks.OfType<TableBlock>().ToList();
        Assert.Single(tables[0].Rows);
        Assert.Equal(1, tables[0].HeaderRows);
        Assert.Equal(4, tables[1].Rows.Count);
        Assert.Equal(2, tables[1].HeaderRows);
    }

    [Fact]
    public void MergingTablesWithTheSameHeadingDiscardsTheNotesOfTheDroppedHeading()
    {
        var first = Grid("t", "h", "a");
        var second = Grid("u", "h", "b");
        first.HeaderRows = 1;
        second.HeaderRows = 1;
        var heading = (ParagraphBlock)second.Rows[0].Cells[0].Content[0];
        heading.State = heading.State with { Marks = [new NoteMark(1, "n1")] };
        _f.Document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np", "nota")])));
        _f.Document.Body.Replace([first, second]);
        var at = new TableEditing(_f.Stories, _f.Deletion).Locate(_f.Document, "tr0")!;

        Conversion().MergeWithNext(_f.Document, _f.History, at);

        var merged = Assert.Single(_f.Document.Body.Blocks.OfType<TableBlock>());
        Assert.Equal(3, merged.Rows.Count);
        Assert.Null(_f.Document.Notes.Find("n1"));
        _f.History.Undo();
        Assert.NotNull(_f.Document.Notes.Find("n1"));
    }
}

using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class FieldRoundOneHundredFiveTests
{
    private readonly FieldRunRewriter _rewriter = new();

    [Fact]
    public void AMarkInsideAFieldThatShrinksStaysInsideTheParagraph()
    {
        var before = new List<TextRun> { new("ab"), new("12345") { Field = TextFieldKind.PageCount } };
        var state = new ParagraphState(new TextParagraph(string.Empty).WithRuns(before), [new BookmarkMark(5, "inside", MarkEdge.Start)]);

        var after = _rewriter.Rewrite(state, [before[0], before[1] with { Text = "9" }]);

        Assert.Equal(3, after.Marks.Single().Offset);
    }

    [Fact]
    public void MarksAfterAndBeforeAChangedFieldMoveAsBefore()
    {
        var before = new List<TextRun> { new("ab"), new("1") { Field = TextFieldKind.PageCount }, new("cd") };
        var state = new ParagraphState(new TextParagraph(string.Empty).WithRuns(before),
            [new BookmarkMark(1, "before", MarkEdge.Start), new BookmarkMark(3, "edge", MarkEdge.End), new BookmarkMark(5, "after", MarkEdge.End)]);

        var after = _rewriter.Rewrite(state, [before[0], before[1] with { Text = "100" }, before[2]]);

        Assert.Equal([1, 5, 7], after.Marks.Select(mark => mark.Offset));
    }

    [Fact]
    public void TenThousandSequenceAndVariableFieldsUpdateInOnePassAndUndoInOneStep()
    {
        var f = new Fixture();
        f.Type("x");
        var runs = new List<TextRun>();
        for (var i = 0; i < 10_000; i++)
        {
            runs.Add(new TextRun("?") { Field = TextFieldKind.Sequence, FieldArgument = "Figure" });
            runs.Add(new TextRun("?") { Field = TextFieldKind.VariableSet, FieldArgument = "v", FieldDetail = new FieldDetail { Format = "v + 1" } });
        }
        f.First.Content = f.First.Content.WithRuns(runs);

        f.FieldUpdater.Update(f.Document, f.History, new FieldUpdate(new NoFieldLayoutFacts(), new DateTime(2026, 11, 7)));

        Assert.Equal("10000", f.First.Content.Runs[^2].Text);
        Assert.Equal("10000", f.First.Content.Runs[^1].Text);
        f.History.Undo();
        Assert.All(f.First.Content.Runs, run => Assert.Equal("?", run.Text));
    }
}

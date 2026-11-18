using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.Editing;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class RoundTwoLimitsTests
{
    [Fact]
    public void AHugeReservedIdentifierDoesNotOverflowTheNextOne()
    {
        var ids = new IdSequence();
        ids.Reserve("p2147483647");
        Assert.Equal("p2147483648", ids.Next("p"));
    }

    [Fact]
    public void AnAbsurdReservedIdentifierIsIgnoredAndTheSequenceStaysPositive()
    {
        var ids = new IdSequence();
        ids.Reserve("p9223372036854775807");
        Assert.Equal("p1", ids.Next("p"));
    }

    [Fact]
    public void AListStyleWithoutLevelsAnswersWithADefaultLevel()
    {
        var style = new ListStyle("Empty", []);
        Assert.Equal(NumberFormat.Arabic, style.Level(3).Format);
    }

    [Fact]
    public void ClearingTheStyleOfVeryManyRunsMergesThemInLinearTime()
    {
        const int count = 80_000;
        ParagraphState mapped = null!;
        new PulseOffice.Testing.GrowthCheck().Linear(size =>
        {
            var runs = Enumerable.Range(0, size).Select(index => new TextRun("abcdefghij") { StyleName = "s" + index }).ToList();
            var state = new ParagraphState(new TextParagraph(string.Empty).WithRuns(runs), []);
            return () => mapped = new ParagraphText().Map(state, 0, size * 10, run => run with { StyleName = null });
        }, count);
        Assert.Single(mapped.Content.Runs);
        Assert.Equal(count * 10, mapped.Content.Text.Length);
    }

    [Fact]
    public void TheWholeRangeOfATableWithoutRowsIsTheFirstCell()
    {
        var grid = new TableGrid(new BlockTree());
        var table = new TableBlock("t1", [], [new Twip(1440)]);
        var whole = grid.Whole(table);
        Assert.Equal((0, 0, 0, 0), (whole.Top, whole.Left, whole.Bottom, whole.Right));
    }
}

using PulseOffice.Documents.Layout.Queries;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Rich.Bidi;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class BidiLayoutTests
{
    private readonly LineOffsets _offsets = new(new TextClusters());

    private LineBox Line(LayoutFixture f) => f.Lines(f.Layout())[0];

    private IReadOnlyList<GlyphRun> Texts(LineBox line) => [.. line.Line.Runs.Where(run => run.Kind == GlyphRunKind.Text)];

    [Fact]
    public void ARightToLeftParagraphStartsAtTheRightEdge()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("אבג", p => p with { Direction = TextDirection.RightToLeft });

        var line = Line(f);
        var run = Assert.Single(Texts(line));

        Assert.True(line.Line.RightToLeft);
        Assert.True(run.RightToLeft);
        Assert.Equal(282, run.X, 3);
        Assert.Equal(300, _offsets.XOf(line, 0), 3);
        Assert.Equal(282, _offsets.XOf(line, 3), 3);
    }

    [Fact]
    public void HitTestingInsideARightToLeftRunCountsFromItsRightEdge()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("אבג", p => p with { Direction = TextDirection.RightToLeft });

        var line = Line(f);

        Assert.Equal(0, _offsets.OffsetAt(line, 299.5));
        Assert.Equal(1, _offsets.OffsetAt(line, 293));
        Assert.Equal(3, _offsets.OffsetAt(line, 282.5));
        Assert.Equal(3, _offsets.OffsetAt(line, 10));
        Assert.Equal((0, 1), _offsets.SpanBetween(line, 295, 299));
    }

    [Fact]
    public void AHebrewWordInALatinParagraphIsMirroredInPlace()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("ab אבג cd");

        var line = Line(f);
        var hebrew = Texts(line).Single(run => run.RightToLeft);

        Assert.False(line.Line.RightToLeft);
        Assert.Equal(15, hebrew.X, 3);
        Assert.Equal(15, _offsets.XOf(line, 3), 3);
        Assert.Equal(27, _offsets.XOf(line, 4), 3);
        Assert.Equal(15, _offsets.XOf(line, 6), 3);
    }

    [Fact]
    public void LatinInsideARightToLeftParagraphIsPlacedVisually()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("abc אבג", p => p with { Direction = TextDirection.RightToLeft });

        var runs = Texts(Line(f));

        Assert.Equal(282, runs.Single(run => run.Text == "abc").X, 3);
        Assert.Equal(261, runs.Single(run => run.Text == "אבג").X, 3);
        Assert.Equal(2, runs.Single(run => run.Text == "abc").Level);
    }

    [Fact]
    public void BracketsAtAnOddLevelAreMirrored()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("(א)", p => p with { Direction = TextDirection.RightToLeft });

        var runs = Texts(Line(f)).OrderBy(run => run.X).ToList();

        Assert.Equal(["(", "א", ")"], runs.Select(run => run.Text));
        Assert.Equal(2, runs[0].SourceOffset);
    }

    [Fact]
    public void EndAlignmentOfARightToLeftParagraphIsOnTheLeft()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("אבג", p => p with { Direction = TextDirection.RightToLeft, Align = TextAlign.Right, Explicit = ParagraphOverrides.Align });

        Assert.Equal(0, Texts(Line(f))[0].X, 3);
    }

    [Fact]
    public void TheStartIndentOfARightToLeftParagraphIsOnTheRight()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("אבג", p => p with { Direction = TextDirection.RightToLeft, MarginLeft = 20 });

        var run = Texts(Line(f))[0];

        Assert.Equal(280, run.X + run.Width, 3);
    }

    [Fact]
    public void TheBackgroundOfARightToLeftParagraphLeavesTheStartIndentOnTheRight()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("אבג", p => p with { Direction = TextDirection.RightToLeft, MarginLeft = 20, Background = 0xFFFFFF00 });

        var background = f.Layout().Pages[0].Decorations.Single(box => box.Kind == DecorationKind.ParagraphBackground);

        Assert.Equal(0, background.Bounds.X, 3);
        Assert.Equal(280, background.Bounds.Width, 3);
    }

    [Fact]
    public void ARightToLeftRunEmbedsItsNeutrals()
    {
        var f = new LayoutFixture(width: 300);
        f.AddRuns(new TextRun("ab "), new TextRun("1 2") { RightToLeft = true });

        var runs = Texts(Line(f));

        Assert.All(runs.Where(run => run.SourceOffset >= 3), run => Assert.True(run.Level >= 1));
    }

    [Fact]
    public void LatinParagraphsKeepTheirLogicalLayout()
    {
        var f = new LayoutFixture(width: 300);
        f.Add("abc def");

        var line = Line(f);

        Assert.False(line.Line.Bidirectional);
        Assert.All(line.Line.Runs, run => Assert.Equal(0, run.Level));
    }
}

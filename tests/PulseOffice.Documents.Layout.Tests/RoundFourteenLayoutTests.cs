using System.Diagnostics;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

[Collection(nameof(IncrementalTests))]
public sealed class RoundFourteenLayoutTests(ITestOutputHelper output)
{
    private string Words(int count) => string.Join(' ', Enumerable.Range(0, count).Select(index => "parola" + index % 7));

    [Fact]
    public void AParagraphSpanningHundredsOfPagesIsComposedInLinearTime()
    {
        var f = new LayoutFixture(width: 420, height: 600);
        f.Add(Words(120000));
        var watch = Stopwatch.StartNew();
        var layout = f.Layout();
        var elapsed = watch.Elapsed.TotalMilliseconds;
        output.WriteLine($"{layout.Pages.Count} pages in {elapsed:F0} ms");
        Assert.True(layout.Pages.Count > 200, $"pages: {layout.Pages.Count}");
        Assert.True(elapsed < 8000, $"layout took {elapsed:F0} ms");
    }

    [Fact]
    public void ChainedFramesResumeAfterTheLastCharacterEvenWithAnInlineObjectBeforeIt()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("testo");
        var inner = new ParagraphBlock("fp", new TextParagraph(Words(12)) { Runs = [new TextRun(Words(12))] });
        inner.State = inner.State with { Marks = [new PulseOffice.Documents.Marks.ObjectMark(0, "pic")] };
        f.Document.Objects.Add(new AnchoredObject("a", new ParagraphAnchor(block.Id), new TextBoxContent(new BlockContainer([inner])) { AutoGrowHeight = false, NextFrameName = "b" }, new DocumentSize(Twip.FromPoints(60), Twip.FromPoints(36)))
        {
            Wrap = WrapMode.Through,
        });
        f.Document.Objects.Add(new AnchoredObject("b", new ParagraphAnchor(block.Id), new TextBoxContent(new BlockContainer([new ParagraphBlock("empty", string.Empty)])), new DocumentSize(Twip.FromPoints(120), Twip.FromPoints(12)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(200), Twip.FromPoints(100)),
            Wrap = WrapMode.Through,
        });
        f.Document.Objects.Add(new AnchoredObject("pic", new AsCharacterAnchor("fp"), new TextBoxContent(new BlockContainer()), new DocumentSize(Twip.FromPoints(4), Twip.FromPoints(4))));

        var page = f.Layout().Pages[0];
        var first = page.Objects.Single(box => box.ObjectId == "a");
        var second = page.Objects.Single(box => box.ObjectId == "b");

        Assert.NotEmpty(second.Lines);
        Assert.Equal(first.Lines.Last().Line.End, second.Lines.First().Line.Start);
    }

    [Fact]
    public void ALongBalancedSectionIsLaidOutInLinearTime()
    {
        var f = new LayoutFixture(width: 420, height: 600);
        var content = new BlockContainer();
        for (var index = 0; index < 20000; index++) content.Add(new ParagraphBlock(f.Document.NextId("s"), new TextParagraph(Words(8)) { Runs = [new TextRun(Words(8))] }));
        f.Document.Body.Add(new SectionBlock(f.Document.NextId("sec"), "Columns", content) { Columns = new SectionColumns(2, Twip.FromPoints(10)) });
        var watch = Stopwatch.StartNew();
        var layout = f.Layout();
        var elapsed = watch.Elapsed.TotalMilliseconds;
        output.WriteLine($"{layout.Pages.Count} pages in {elapsed:F0} ms");
        Assert.True(layout.Pages.Count > 200, $"pages: {layout.Pages.Count}");
        Assert.True(elapsed < 8000, $"layout took {elapsed:F0} ms");
    }

    [Fact]
    public void ATableCellSpanningHundredsOfPagesIsLaidOutInLinearTime()
    {
        var f = new LayoutFixture(width: 420, height: 600);
        var content = new BlockContainer();
        for (var index = 0; index < 12000; index++) content.Add(new ParagraphBlock(f.Document.NextId("c"), new TextParagraph(Words(8)) { Runs = [new TextRun(Words(8))] }));
        var other = new BlockContainer([new ParagraphBlock(f.Document.NextId("c"), new TextParagraph("x") { Runs = [new TextRun("x")] })]);
        f.Document.Body.Add(new TableBlock("big", [new TableRow([new TableCell(content), new TableCell(other)])], [Twip.FromPoints(300), Twip.FromPoints(100)]));
        var watch = Stopwatch.StartNew();
        var layout = f.Layout();
        var elapsed = watch.Elapsed.TotalMilliseconds;
        output.WriteLine($"{layout.Pages.Count} pages in {elapsed:F0} ms");
        Assert.True(layout.Pages.Count > 200, $"pages: {layout.Pages.Count}");
        Assert.True(elapsed < 8000, $"layout took {elapsed:F0} ms");
    }

    [Fact]
    public void AParagraphWithManyRunsIsComposedInLinearTime()
    {
        var f = new LayoutFixture(width: 420, height: 600);
        var runs = Enumerable.Range(0, 40000).Select(index => new TextRun("parola" + index % 7 + " ") { Bold = index % 2 == 0 }).ToArray();
        f.AddRuns(runs);
        var watch = Stopwatch.StartNew();
        var layout = f.Layout();
        var elapsed = watch.Elapsed.TotalMilliseconds;
        output.WriteLine($"{layout.Pages.Count} pages in {elapsed:F0} ms");
        Assert.True(elapsed < 8000, $"layout took {elapsed:F0} ms");
    }
}

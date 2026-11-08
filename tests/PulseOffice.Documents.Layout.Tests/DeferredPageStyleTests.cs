using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class DeferredPageStyleTests
{
    private string Words(int count) => string.Join(' ', Enumerable.Range(0, count).Select(_ => "aaaaaaaaa"));

    private string Text(LineBox line) => string.Concat(line.Line.Runs.Where(run => run.Kind == GlyphRunKind.Text).Select(run => run.Text)).TrimEnd();

    private LayoutFixture WithHeaders()
    {
        var f = new LayoutFixture(width: 200, height: 120);
        foreach (var (name, text) in new[] { (LayoutFixture.Page, "old"), ("Next", "new") })
        {
            var style = f.Document.Styles.Page.Find(name) ?? f.PageOf(name, 200, 120);
            style.Header.Enabled = true;
            style.Header.Spacing = Twip.Zero;
            style.Header.Default.Add(new ParagraphBlock(f.Document.NextId("h"), new TextParagraph(text) { Runs = [new TextRun(text)] }));
            if (f.Document.Styles.Page.Find(name) is null) f.Document.Styles.Page.Add(style);
        }
        return f;
    }

    [Fact]
    public void ADeferredPageStyleStaysOnTheCurrentPageAndAppliesFromTheNextOne()
    {
        var f = WithHeaders();
        f.Add("one");
        f.Add("two", p => p with { NextPageStyleName = "Next" });
        for (var index = 0; index < 20; index++) f.Add("line");

        var layout = f.Layout();

        Assert.True(layout.Pages.Count >= 3);
        Assert.Equal(["one", "two"], layout.Pages[0].Lines.Take(2).Select(Text));
        Assert.Equal(LayoutFixture.Page, layout.Pages[0].StyleName);
        Assert.All(layout.Pages.Skip(1), page => Assert.Equal("Next", page.StyleName));
        Assert.Equal("old", Text(layout.Pages[0].Header!.Lines.Single()));
        Assert.All(layout.Pages.Skip(1), page => Assert.Equal("new", Text(page.Header!.Lines.Single())));
    }

    [Fact]
    public void ADeferredPageStyleAtTheTopOfAPageAppliesToThatPage()
    {
        var f = WithHeaders();
        f.Add("one");
        f.Add("two", p => p with { PageBreakBefore = true });
        f.Add("three", p => p with { NextPageStyleName = "Next" });

        var layout = f.Layout();

        Assert.Equal(2, layout.Pages.Count);
        Assert.Equal([LayoutFixture.Page, LayoutFixture.Page], layout.Pages.Select(page => page.StyleName));

        var g = WithHeaders();
        g.Add("one");
        g.Add("two", p => p with { NextPageStyleName = "Next", PageBreakBefore = true });

        Assert.Equal([LayoutFixture.Page, "Next"], g.Layout().Pages.Select(page => page.StyleName));
    }

    [Fact]
    public void ADeferredPageNumberRestartAppliesWithTheNextPage()
    {
        var f = WithHeaders();
        f.Add("zero");
        f.Add("one", p => p with { NextPageStyleName = "Next", PageNumberRestart = 5 });
        f.Add(Words(200));

        var layout = f.Layout();

        Assert.True(layout.Pages.Count >= 2);
        Assert.Equal(5, layout.Pages[1].Start.Number);
    }
}

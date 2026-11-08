using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Foundation.Units;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class EdgeProbeTests
{
    private static async Task Bounded(Action action)
    {
        var task = Task.Run(action);
        var done = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.True(done == task, "layout did not finish");
        await task;
    }

    [Fact]
    public Task UnaPaginaLargaZeroNonBlocca() => Bounded(() =>
    {
        var f = new LayoutFixture(width: 0, height: 100);
        f.Add("uno due tre quattro cinque");
        Assert.NotEmpty(f.Layout().Pages);
    });

    [Fact]
    public Task MarginiPiuGrandiDellaPaginaNonBloccano() => Bounded(() =>
    {
        var f = new LayoutFixture(width: 100, height: 100);
        var style = f.PageOf("Wide", 100, 100);
        style.Margins = new Margins(Twip.FromPoints(80), Twip.FromPoints(80), Twip.FromPoints(80), Twip.FromPoints(80));
        f.Document.Styles.Page.Add(style);
        f.Document.InitialPageStyle = "Wide";
        f.Add("uno due tre quattro cinque sei sette otto");
        Assert.NotEmpty(f.Layout().Pages);
    });

    [Fact]
    public Task UnCorpoEnormeNonBlocca() => Bounded(() =>
    {
        var f = new LayoutFixture(width: 100, height: 100);
        f.Add("uno due tre", p => p with { FontSize = 5000 });
        f.Add("quattro");
        var layout = f.Layout();
        Assert.All(f.Lines(layout), line => Assert.True(double.IsFinite(line.Top)));
    });

    [Fact]
    public Task RientriAssurdiNonBloccano() => Bounded(() =>
    {
        var f = new LayoutFixture(width: 100, height: 100);
        f.Add("uno due tre quattro cinque", p => p with { MarginLeft = 500, Indent = -900, MarginRight = 400 });
        f.Add("uno due\tx\ty", p => p with { TabStops = [new TabStop(-5), new TabStop(1e9), new TabStop(double.NaN)] });
        f.Add("sei", p => p with { LineSpacing = 0, SpaceBefore = double.NaN, Explicit = ParagraphOverrides.LineSpacing | ParagraphOverrides.SpaceBefore });
        var layout = f.Layout();
        Assert.All(f.Lines(layout), line => Assert.True(double.IsFinite(line.Top)));
    });
}

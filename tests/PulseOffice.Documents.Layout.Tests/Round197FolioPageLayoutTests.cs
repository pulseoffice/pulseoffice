using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round197FolioPageLayoutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mirrored_pages_swap_the_inner_and_outer_margins_on_even_pages(bool header)
    {
        var f = new LayoutFixture(width: 200, height: 60);
        var style = f.Document.Styles.Page.Find(LayoutFixture.Page)!;
        style.Margins = new Margins(Twip.FromPoints(20), Twip.Zero, Twip.Zero, Twip.Zero);
        style.Usage = PageUsage.Mirrored;
        if (header) style.Header.Enabled = true;
        for (var i = 0; i < 12; i++) f.Add("riga");

        var layout = f.Layout();

        Assert.True(layout.Pages.Count >= 2);
        Assert.Equal(20, layout.Pages[0].Content.X, 3);
        Assert.Equal(0, layout.Pages[1].Content.X, 3);
    }
}

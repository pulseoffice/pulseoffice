using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round242InsideOutsideRelayoutTests
{
    private string Words(int count) => string.Join(' ', Enumerable.Range(0, count).Select(_ => "aaaaaaaaa"));

    [Fact]
    public void An_outside_aligned_object_follows_the_page_parity_after_an_incremental_relayout()
    {
        var f = new LayoutFixture(width: 200, height: 100);
        for (var i = 0; i < 12; i++) f.Add(Words(6));
        var anchor = f.Document.Body.Blocks.OfType<ParagraphBlock>().Last();
        f.Document.Objects.Add(new AnchoredObject("o", new ParagraphAnchor(anchor.Id), new TextBoxContent(new BlockContainer()), new DocumentSize(Twip.FromPoints(20), Twip.FromPoints(10)))
        {
            Placement = new ObjectPlacement(Twip.Zero, Twip.Zero) { Horizontal = HorizontalRelation.Page, HorizontalAlign = ObjectAlignment.Outside },
            Wrap = WrapMode.Through,
        });
        var previous = f.Layout();
        var first = (ParagraphBlock)f.Document.Body.Blocks.OfType<ParagraphBlock>().First();
        first.Content = first.Content.WithRuns([new TextRun(Words(40))]);

        var incremental = f.Engine.Relayout(f.Document, previous);
        f.Engine.Invalidate();
        var full = f.Engine.Layout(f.Document);

        Assert.Equal(full.Pages.SelectMany(page => page.Objects).Select(item => item.Bounds), incremental.Pages.SelectMany(page => page.Objects).Select(item => item.Bounds));
    }
}

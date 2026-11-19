using PulseOffice.Foundation.Units;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Testing;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round272TextShrinkScaleTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    [Fact]
    public Task A_text_box_that_shrinks_stores_the_recomputed_font_scale() => Session.Dispatch(() =>
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        var paragraphs = Enumerable.Range(1, 12).Select(index => new TextParagraph($"Riga {index}")).ToList();
        var box = new TextElement
        {
            Id = "t",
            Text = new RichText(PlaceholderRole.Free, paragraphs) { Frame = new TextFrame { Autofit = TextAutofit.ShrinkText, FontScale = 1 } },
            Bounds = new DocumentRect(new Twip(0), new Twip(0), Twip.FromCentimeters(8), Twip.FromCentimeters(2)),
        };
        var fitting = new TextFrameFitting(new TextFrameMeasure(new ListLabels()), new TextFrameResize());

        var fitted = (TextElement)fitting.Fit(deck, slide, box);

        Assert.True(fitted.Text.Frame.FontScale < 0.9);
        Assert.Equal(box.Bounds, fitted.Bounds);
    }, CancellationToken.None);
}

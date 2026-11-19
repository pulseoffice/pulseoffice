using Avalonia;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Testing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round238SlideLinkTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    private static TextElement Text(string id, params TextRun[] runs) => new()
    {
        Id = id,
        Text = new RichText(PlaceholderRole.Free, [new TextParagraph(string.Concat(runs.Select(run => run.Text))) { Runs = runs }]),
        Bounds = new DocumentRect(new Twip(100), new Twip(200), new Twip(6000), new Twip(1400)),
    };

    private static (Presentation Deck, Slide Slide) Deck()
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var slide = new Slide("s1", SlideLayout.Blank);
        deck.AddExisting(slide);
        return (deck, slide);
    }

    private static SlideLinkPlacement Placement(Presentation deck, Slide slide) =>
        new(deck, slide, new Rect(0, 0, deck.Size.Width.Value / 20.0, deck.Size.Height.Value / 20.0), _ => true);

    [Fact]
    public Task Shape_hyperlinks_and_every_linked_run_become_link_areas() => Session.Dispatch(() =>
    {
        var (deck, slide) = Deck();
        slide.Add(Text("shape", new TextRun("Go")));
        slide.Add(Text("mixed", new TextRun("WWWW") { Hyperlink = "https://a.example" }, new TextRun(" plain "), new TextRun("WWWW") { Hyperlink = "https://b.example" }));
        slide.AddInteraction(new InteractiveAction { TargetId = "shape", Kind = InteractionKind.Hyperlink, Reference = "https://shape.example" });

        var areas = new SlideLinkAreas().For(Placement(deck, slide));

        Assert.Equal(["https://shape.example", "https://a.example", "https://b.example"], areas.Select(area => area.Target));
        Assert.True(areas[1].Area.Right.Value <= areas[2].Area.Left.Value);
        Assert.True(areas[1].Area.Width.Value < 6000 / 2);
    }, CancellationToken.None);
}

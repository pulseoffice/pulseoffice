using Avalonia;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round244SlideNavigationLinkTests
{
    private static ObjectElement Button(string id) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(100), new Twip(100), new Twip(400), new Twip(200)),
        Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = id + "-shape", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle },
    };

    [Fact]
    public void Navigation_actions_link_to_the_exported_slides_by_name()
    {
        var deck = new Presentation();
        var first = new Slide("a", SlideLayout.Blank);
        var hidden = new Slide("b", SlideLayout.Blank) { Hidden = true };
        var last = new Slide("c", SlideLayout.Blank);
        deck.AddExisting(first);
        deck.AddExisting(hidden);
        deck.AddExisting(last);
        foreach (var (id, kind, reference) in new[] { ("next", InteractionKind.NextSlide, (string?)null), ("prev", InteractionKind.PreviousSlide, null), ("go", InteractionKind.GoToSlide, "c"), ("gone", InteractionKind.GoToSlide, "b"), ("end", InteractionKind.LastSlide, null) })
        {
            first.Add(Button(id));
            first.AddInteraction(new InteractiveAction { TargetId = id, Kind = kind, Reference = reference });
        }

        var areas = new SlideLinkAreas().For(new SlideLinkPlacement(deck, first, new Rect(0, 0, 960, 540), slide => !slide.Hidden));

        Assert.Equal(["#slide-c", "#slide-c", "#slide-c"], areas.Select(area => area.Target));
    }

    [Fact]
    public void Slide_pages_carry_the_deck_numbering_as_labels()
    {
        var setup = new PageSetup { Paper = new PaperCatalog().Default };
        var printable = new DeckPrintable(setup, [], [], DeckExportLayout.Slides) { Numbers = [5, 6] };

        Assert.Equal(new PrintPageLabel(PageLabelStyle.Decimal, 5), printable.PageLabel(0));
        Assert.Null(new DeckPrintable(setup, [], [], DeckExportLayout.Handout4) { Numbers = [5] }.PageLabel(0));
    }
}

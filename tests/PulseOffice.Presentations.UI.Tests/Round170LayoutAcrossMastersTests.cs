using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round170LayoutAcrossMastersTests
{
    [Fact]
    public void Applying_the_same_kind_of_layout_from_another_master_moves_the_placeholders_in_one_step()
    {
        var deck = new Presentation();
        var history = new UndoStack();
        var factory = new SlideFactory();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var actions = new DeckActions(deck, history, factory, new FakeTextCatalog());
        deck.AddExisting(factory.Create(deck.NextId(), deck.Master, SlideLayout.TitleAndContent));
        var shifted = new DocumentRect(new Twip(100), new Twip(200), new Twip(3000), new Twip(900));
        var second = deck.Master with
        {
            Id = "second",
            Layouts = [.. deck.Master.Layouts.Select(layout => layout with
            {
                Id = "second-" + layout.Id,
                Placeholders = [.. layout.Placeholders.Select(placeholder => placeholder.Role == PlaceholderRole.Title ? placeholder with { Bounds = shifted } : placeholder)],
            })],
        };
        deck.AddMaster(second);
        var before = deck.Slides[0].Placeholder(PlaceholderRole.Title)!.Bounds;

        actions.Layouts.ApplyLayout(deck.Slides[0], second, second.Template(SlideLayout.TitleAndContent));

        Assert.Equal("second", deck.Slides[0].MasterId);
        Assert.Equal(second.Template(SlideLayout.TitleAndContent).Id, deck.Slides[0].LayoutId);
        Assert.Equal(shifted, deck.Slides[0].Placeholder(PlaceholderRole.Title)!.Bounds);
        history.Undo();
        Assert.Null(deck.Slides[0].MasterId);
        Assert.Equal(before, deck.Slides[0].Placeholder(PlaceholderRole.Title)!.Bounds);
        Assert.False(history.CanUndo);
    }
}

using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round329MasterEditingTests
{
    private static (Presentation Deck, UndoStack History, DeckActions Actions, SlideMaster Second) TwoMasters()
    {
        var deck = new Presentation();
        var history = new UndoStack();
        var factory = new SlideFactory();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var second = deck.Master with
        {
            Id = "second",
            Name = "Second",
            Layouts = [.. deck.Master.Layouts.Select(layout => layout with { Id = "second-" + layout.Id })],
        };
        deck.AddMaster(second);
        return (deck, history, new DeckActions(deck, history, factory, new FakeTextCatalog()), second);
    }

    [Fact]
    public void Moving_one_of_two_body_placeholders_changes_only_that_one_and_the_slides_that_follow_it()
    {
        var (deck, history, actions, second) = TwoMasters();
        var layout = second.Template(SlideLayout.TwoContent);
        var slide = new SlideFactory().Create(deck.NextId(), layout);
        slide.MasterId = second.Id;
        deck.AddExisting(slide);
        var bodies = layout.Placeholders.Select((placeholder, index) => (placeholder, index)).Where(entry => entry.placeholder.Role == PlaceholderRole.Body).ToList();
        Assert.True(bodies.Count >= 2);
        var moved = new DocumentRect(new Twip(10), new Twip(20), new Twip(3000), new Twip(2000));
        var primaryBefore = deck.Master;

        actions.SetPlaceholderBounds(second, layout.Id, bodies[1].index, moved);

        var edited = deck.MasterById("second").Template(SlideLayout.TwoContent);
        Assert.Equal(bodies[0].placeholder.Bounds, edited.Placeholders[bodies[0].index].Bounds);
        Assert.Equal(moved, edited.Placeholders[bodies[1].index].Bounds);
        Assert.Same(primaryBefore, deck.Master);
        var slideBodies = deck.Slides[0].Elements.OfType<TextElement>().Where(text => text.Text.Role == PlaceholderRole.Body).ToList();
        Assert.Equal(bodies[0].placeholder.Bounds, slideBodies[0].Bounds);
        Assert.Equal(moved, slideBodies[1].Bounds);

        history.Undo();
        Assert.Equal(bodies[1].placeholder.Bounds, deck.Slides[0].Elements.OfType<TextElement>().Where(text => text.Text.Role == PlaceholderRole.Body).ElementAt(1).Bounds);
        Assert.Equal(bodies[1].placeholder.Bounds, deck.MasterById("second").Template(SlideLayout.TwoContent).Placeholders[bodies[1].index].Bounds);
    }

    [Fact]
    public void A_secondary_master_can_be_renamed_and_recoloured_in_one_step_each()
    {
        var (deck, history, actions, second) = TwoMasters();

        actions.RenameMaster(second, "  Dark  ");
        Assert.Equal("Dark", deck.MasterById("second").Name);
        actions.SetMasterBackground(deck.MasterById("second"), ColorRole.Accent1);
        Assert.Equal(ColorRole.Accent1, deck.MasterById("second").Background);
        Assert.Equal(ColorRole.Background, deck.Master.Background);

        history.Undo();
        history.Undo();
        Assert.Equal("Second", deck.MasterById("second").Name);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Slides_inserted_keeping_source_formatting_bring_their_master_and_layout_in_one_step()
    {
        var (deck, history, actions, _) = TwoMasters();
        var source = new Presentation { Theme = new Theme { Name = "Source" } };
        var factory = new SlideFactory();
        source.Master = new MasterFactory().CreateStandard(source.Theme) with { Name = "Source master" };
        var custom = source.Master.Template(SlideLayout.TitleOnly) with { Id = "custom", Name = "Custom" };
        source.Master = source.Master with { Layouts = [.. source.Master.Layouts, custom] };
        source.AddExisting(factory.Create(source.NextId(), custom));
        var masters = deck.Masters.Count;

        actions.ImportSlidesKeepingFormatting(-1, source, source.Slides.ToList());

        Assert.Equal(masters + 1, deck.Masters.Count);
        var inserted = deck.Slides[0];
        var adopted = deck.MasterOf(inserted);
        Assert.Equal("Source master", adopted.Name);
        Assert.Equal("Source", deck.ThemeOf(inserted).Name);
        Assert.Equal("custom", adopted.Template(inserted).Id);
        history.Undo();
        Assert.Equal(masters, deck.Masters.Count);
        Assert.Empty(deck.Slides);
    }

    [Fact]
    public void Pasted_slides_keep_their_master_in_the_same_deck_and_take_the_destination_master_elsewhere()
    {
        var (deck, history, actions, second) = TwoMasters();
        var slide = new SlideFactory().Create(deck.NextId(), second.Template(SlideLayout.TitleOnly));
        slide.MasterId = second.Id;
        deck.AddExisting(slide);

        actions.PasteSlides(0, deck, [slide]);
        Assert.Equal(2, deck.Slides.Count);
        Assert.Equal(second.Id, deck.Slides[1].MasterId);
        Assert.NotEqual(slide.Id, deck.Slides[1].Id);
        history.Undo();
        Assert.Single(deck.Slides);

        var (other, _, otherActions, _) = TwoMasters();
        otherActions.PasteSlides(-1, deck, [slide]);
        Assert.Null(Assert.Single(other.Slides).MasterId);
    }

    [Fact]
    public void Changing_the_master_takes_the_new_master_layout_geometry_in_one_step()
    {
        var (deck, history, actions, second) = TwoMasters();
        var shifted = new DocumentRect(new Twip(300), new Twip(400), new Twip(5000), new Twip(1000));
        var moved = new MasterEditing().WithPlaceholderBounds(second, SlideLayout.TitleAndContent, PlaceholderRole.Title, shifted);
        deck.ReplaceMaster(moved);
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.TitleAndContent));
        var before = deck.Slides[0].Placeholder(PlaceholderRole.Title)!.Bounds;

        actions.Layouts.ChangeMaster([deck.Slides[0]], moved);

        Assert.Equal("second", deck.Slides[0].MasterId);
        Assert.Equal(moved.Template(SlideLayout.TitleAndContent).Id, deck.Slides[0].LayoutId);
        Assert.Equal(shifted, deck.Slides[0].Placeholder(PlaceholderRole.Title)!.Bounds);
        history.Undo();
        Assert.Null(deck.Slides[0].MasterId);
        Assert.Equal(before, deck.Slides[0].Placeholder(PlaceholderRole.Title)!.Bounds);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Deleting_a_master_moves_its_slides_to_the_main_master_layouts_in_one_step()
    {
        var (deck, history, actions, second) = TwoMasters();
        var shifted = new DocumentRect(new Twip(300), new Twip(400), new Twip(5000), new Twip(1000));
        second = new MasterEditing().WithPlaceholderBounds(second, SlideLayout.TitleAndContent, PlaceholderRole.Title, shifted);
        deck.ReplaceMaster(second);
        var slide = new SlideFactory().Create(deck.NextId(), second.Template(SlideLayout.TitleAndContent));
        slide.MasterId = second.Id;
        deck.AddExisting(slide);

        actions.Layouts.DeleteMaster(second);

        Assert.Single(deck.Masters);
        Assert.Null(deck.Slides[0].MasterId);
        Assert.Equal(deck.Master.Template(SlideLayout.TitleAndContent).Id, deck.Slides[0].LayoutId);
        Assert.NotEqual(shifted, deck.Slides[0].Placeholder(PlaceholderRole.Title)!.Bounds);
        history.Undo();
        Assert.Equal(2, deck.Masters.Count);
        Assert.Equal(second.Id, deck.Slides[0].MasterId);
        Assert.Equal(shifted, deck.Slides[0].Placeholder(PlaceholderRole.Title)!.Bounds);
        Assert.False(history.CanUndo);
    }
}

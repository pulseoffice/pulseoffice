using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round234EmptySectionTests
{
    private readonly Presentation _deck = new();
    private readonly UndoStack _history = new();
    private readonly DeckActions _actions;
    private readonly ISlideFactory _factory = new SlideFactory();

    public Round234EmptySectionTests()
    {
        _deck.Master = new MasterFactory().CreateStandard(_deck.Theme);
        _actions = new DeckActions(_deck, _history, _factory, new FakeTextCatalog());
        for (var i = 0; i < 3; i++) _deck.AddExisting(_factory.Create(_deck.NextId(), _deck.Master, SlideLayout.Blank));
        _deck.EmptySections.Add(new EmptySection("Empty", "s1", _deck.Slides[1].Id));
        _deck.EmptySections.Add(new EmptySection("Tail", "s2", null));
    }

    [Fact]
    public void The_strip_layout_shows_empty_sections_as_headers_in_place()
    {
        var items = new SlideStripLayout().Items(_deck);

        Assert.Equal(["slide", "Empty", "slide", "slide", "Tail"], items.Select(item => item.Header?.Name ?? "slide"));
        Assert.True(items[1].Header!.IsEmpty);
        Assert.Equal(1, items[2].SlideIndex);
    }

    [Fact]
    public void An_empty_section_stays_in_place_when_the_slide_after_it_moves()
    {
        var moved = _deck.Slides[1];
        var following = _deck.Slides[2];

        _actions.MoveSlide(moved, 0);
        Assert.Equal(following.Id, _deck.EmptySections[0].BeforeSlideId);

        _history.Undo();
        Assert.Equal(moved.Id, _deck.EmptySections[0].BeforeSlideId);
    }

    [Fact]
    public void Empty_sections_are_renamed_and_removed_as_undoable_steps()
    {
        _actions.RenameEmptySection(_deck.EmptySections[0], "Renamed");
        Assert.Equal("Renamed", _deck.EmptySections[0].Name);

        _actions.RemoveEmptySection(_deck.EmptySections[1]);
        Assert.Equal("Renamed", Assert.Single(_deck.EmptySections).Name);

        _history.Undo();
        _history.Undo();
        Assert.Equal(["Empty", "Tail"], _deck.EmptySections.Select(section => section.Name));
    }
}

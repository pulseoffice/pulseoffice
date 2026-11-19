using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round240SectionDropTests
{
    private readonly Presentation _deck = new();
    private readonly UndoStack _history = new();
    private readonly DeckActions _actions;
    private readonly ISlideFactory _factory = new SlideFactory();

    public Round240SectionDropTests()
    {
        _deck.Master = new MasterFactory().CreateStandard(_deck.Theme);
        _actions = new DeckActions(_deck, _history, _factory, new FakeTextCatalog());
        for (var i = 0; i < 3; i++) _deck.AddExisting(_factory.Create(_deck.NextId(), _deck.Master, SlideLayout.Blank));
        _deck.Slides[1].SectionName = "Middle";
        _deck.EmptySections.Add(new EmptySection("Empty", "e1", _deck.Slides[1].Id));
        _deck.EmptySections.Add(new EmptySection("Tail", "e2", null));
    }

    [Fact]
    public void A_slide_dropped_on_an_empty_section_becomes_its_first_slide()
    {
        var moved = _deck.Slides[2];

        _actions.MoveSlideIntoSection(moved, _deck.EmptySections[0]);

        Assert.Equal(1, _deck.IndexOf(moved));
        Assert.Equal(("Empty", "e1"), (moved.SectionName, moved.SectionId));
        Assert.Equal("Middle", _deck.Slides[2].SectionName);
        Assert.Equal("Tail", Assert.Single(_deck.EmptySections).Name);

        _history.Undo();
        Assert.Equal(2, _deck.IndexOf(moved));
        Assert.Null(moved.SectionName);
        Assert.Equal(2, _deck.EmptySections.Count);
    }

    [Fact]
    public void A_slide_dropped_on_the_trailing_empty_section_moves_to_the_end()
    {
        var moved = _deck.Slides[0];

        _actions.MoveSlideIntoSection(moved, _deck.EmptySections[1]);

        Assert.Same(moved, _deck.Slides[^1]);
        Assert.Equal("Tail", moved.SectionName);
    }

    [Fact]
    public void Remove_empty_sections_clears_them_in_one_step()
    {
        _actions.RemoveEmptySections();
        Assert.Empty(_deck.EmptySections);

        _history.Undo();
        Assert.Equal(2, _deck.EmptySections.Count);
    }
}

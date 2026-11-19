using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round245EmptySectionNearTests
{
    private readonly Presentation _deck = new();
    private readonly UndoStack _history = new();
    private readonly DeckActions _actions;
    private readonly SlideSections _sections = new();

    public Round245EmptySectionNearTests()
    {
        var factory = new SlideFactory();
        _deck.Master = new MasterFactory().CreateStandard(_deck.Theme);
        _actions = new DeckActions(_deck, _history, factory, new FakeTextCatalog());
        for (var i = 0; i < 3; i++) _deck.AddExisting(factory.Create(_deck.NextId(), _deck.Master, SlideLayout.Blank));
        _deck.EmptySections.Add(new EmptySection("Between", "s1", _deck.Slides[1].Id));
        _deck.EmptySections.Add(new EmptySection("Tail", "s2", null));
    }

    [Fact]
    public void The_empty_section_after_or_before_the_current_slide_is_found_and_acted_on()
    {
        Assert.Equal("Between", _sections.EmptyNear(_deck, _deck.Slides[0])?.Name);
        Assert.Equal("Between", _sections.EmptyNear(_deck, _deck.Slides[1])?.Name);
        Assert.Equal("Tail", _sections.EmptyNear(_deck, _deck.Slides[2])?.Name);

        _actions.RenameEmptySection(_sections.EmptyNear(_deck, _deck.Slides[2])!, "End");
        _actions.RemoveEmptySection(_sections.EmptyNear(_deck, _deck.Slides[0])!);

        Assert.Equal("End", Assert.Single(_deck.EmptySections).Name);
        Assert.Null(_sections.EmptyNear(_deck, _deck.Slides[0]));
        _history.Undo();
        _history.Undo();
        Assert.Equal(["Between", "Tail"], _deck.EmptySections.Select(section => section.Name));
    }
}

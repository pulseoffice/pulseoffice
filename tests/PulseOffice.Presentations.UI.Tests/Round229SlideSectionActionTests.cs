using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round229SlideSectionActionTests
{
    private readonly Presentation _deck = new();
    private readonly UndoStack _history = new();
    private readonly DeckActions _actions;
    private readonly ISlideFactory _factory = new SlideFactory();

    public Round229SlideSectionActionTests()
    {
        _deck.Master = new MasterFactory().CreateStandard(_deck.Theme);
        _actions = new DeckActions(_deck, _history, _factory, new FakeTextCatalog());
        for (var i = 0; i < 2; i++) _deck.AddExisting(_factory.Create(_deck.NextId(), _deck.Master, SlideLayout.Blank));
    }

    [Fact]
    public void Deleting_the_first_slide_of_a_section_hands_its_identity_to_the_next_and_undo_takes_it_back()
    {
        var id = "{1A2B3C4D-5E6F-4A7B-8C9D-0E1F2A3B4C5D}";
        (_deck.Slides[0].SectionName, _deck.Slides[0].SectionId) = ("Intro", id);
        var next = _deck.Slides[1];

        _actions.DeleteSlide(_deck.Slides[0]);
        Assert.Equal("Intro", next.SectionName);
        Assert.Equal(id, next.SectionId);

        _history.Undo();
        Assert.Null(next.SectionName);
        Assert.Null(next.SectionId);
        Assert.Equal(id, _deck.Slides[0].SectionId);
    }
}

using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round231SectionDeleteTests
{
    private readonly Presentation _deck = new();
    private readonly UndoStack _history = new();
    private readonly DeckActions _actions;
    private readonly ISlideFactory _factory = new SlideFactory();

    public Round231SectionDeleteTests()
    {
        _deck.Master = new MasterFactory().CreateStandard(_deck.Theme);
        _actions = new DeckActions(_deck, _history, _factory, new FakeTextCatalog());
        for (var i = 0; i < 3; i++) _deck.AddExisting(_factory.Create(_deck.NextId(), _deck.Master, SlideLayout.Blank));
        _deck.Slides[1].SectionName = "Middle";
        _deck.Slides[2].SectionName = "End";
    }

    [Fact]
    public void Deleting_the_only_slide_of_a_section_keeps_the_section_empty_and_undo_restores_it()
    {
        var end = _deck.Slides[2];

        _actions.DeleteSlide(_deck.Slides[1]);
        var empty = Assert.Single(_deck.EmptySections);
        Assert.Equal("Middle", empty.Name);
        Assert.Equal(end.Id, empty.BeforeSlideId);
        Assert.Equal(new[] { "Middle", "End" }, new SlideSections().Ordered(_deck).Select(section => section.Name));

        _history.Undo();
        Assert.Empty(_deck.EmptySections);
        Assert.Equal("Middle", _deck.Slides[1].SectionName);
    }
}

using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round229OutlineActionTests
{
    private readonly Presentation _deck = new();
    private readonly UndoStack _history = new();
    private readonly DeckActions _actions;

    public Round229OutlineActionTests()
    {
        _deck.Master = new MasterFactory().CreateStandard(_deck.Theme);
        _actions = new DeckActions(_deck, _history, new SlideFactory(), new FakeTextCatalog());
        _deck.AddExisting(new SlideFactory().Create(_deck.NextId(), _deck.Master, SlideLayout.TitleAndContent));
    }

    [Fact]
    public void Demoting_an_outline_line_changes_the_bullet_level_in_one_undo_step()
    {
        var slide = _deck.Slides[0];
        _actions.SetSlideOutline([(slide, "Plan", [new OutlineLine(0, "One"), new OutlineLine(0, "Two")])]);
        _actions.SetSlideOutline([(slide, "Plan", [new OutlineLine(0, "One"), new OutlineLine(1, "Two")])]);

        Assert.Equal([0, 1], slide.Placeholder(PlaceholderRole.Body)!.Text.Paragraphs.Select(paragraph => paragraph.BulletLevel));
        Assert.Equal("Plan", slide.Placeholder(PlaceholderRole.Title)!.Text.PlainText);

        _history.Undo();
        Assert.Equal([0, 0], slide.Placeholder(PlaceholderRole.Body)!.Text.Paragraphs.Select(paragraph => paragraph.BulletLevel));
    }
}

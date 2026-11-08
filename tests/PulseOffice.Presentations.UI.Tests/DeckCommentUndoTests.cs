using PulseOffice.Document.Commands;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckCommentUndoTests
{
    [Fact]
    public void Undoing_a_comment_deletion_puts_it_back_in_its_place()
    {
        var deck = new Presentation();
        var factory = new SlideFactory();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var slide = factory.Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        foreach (var id in new[] { "a", "b", "c" }) slide.AddComment(new SlideComment { Id = id, Author = "Ada", Text = id });
        deck.AddExisting(slide);
        var history = new UndoStack();
        var actions = new DeckActions(deck, history, factory, new FakeTextCatalog());

        actions.RemoveComments([(slide, slide.Comments[0]), (slide, slide.Comments[1])]);
        Assert.Equal(["c"], slide.Comments.Select(comment => comment.Id));

        history.Undo();

        Assert.Equal(["a", "b", "c"], slide.Comments.Select(comment => comment.Id));
    }
}

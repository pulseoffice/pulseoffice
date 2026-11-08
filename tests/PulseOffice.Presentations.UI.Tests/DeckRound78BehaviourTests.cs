using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckRound78BehaviourTests
{
    private readonly ISlideFactory _factory = new SlideFactory();

    [Fact]
    public void Duplicating_an_element_carries_its_animations_and_action_in_one_undo_step()
    {
        var (deck, history, actions, slide) = Deck();
        var shape = Shape("box");
        slide.Add(shape);
        slide.AddAnimation(new AnimationEffect { Id = "a1", TargetId = "box", Preset = "fly-in" });
        slide.AddAnimation(new AnimationEffect { Id = "a2", TargetId = "other" });
        slide.SetInteraction(new InteractiveAction { TargetId = "box", Kind = InteractionKind.NextSlide });

        var clones = actions.AddClones(slide, [shape], 200, 200, "deck.action.duplicateObject", slide.Groups, actions.BehavioursOf(slide, [shape]));

        var clone = Assert.Single(clones);
        var carried = Assert.Single(slide.Animations, effect => effect.TargetId == clone.Id);
        Assert.Equal("fly-in", carried.Preset);
        Assert.Equal(slide.Animations.Count, slide.Animations.Select(effect => effect.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(InteractionKind.NextSlide, slide.Interaction(clone.Id)?.Kind);

        history.Undo();
        Assert.Equal(2, slide.Animations.Count);
        Assert.Null(slide.Interaction(clone.Id));
        Assert.NotNull(slide.Interaction("box"));
        Assert.False(history.CanUndo);

        history.Redo();
        Assert.Contains(slide.Animations, effect => effect.TargetId == clone.Id);
        Assert.NotNull(deck);
    }

    [Fact]
    public void Pasting_into_another_deck_remaps_the_carried_behaviours()
    {
        var (_, _, sourceActions, source) = Deck();
        var shape = Shape("box");
        source.Add(shape);
        source.AddAnimation(new AnimationEffect { Id = "a1", TargetId = "box" });
        source.SetInteraction(new InteractiveAction { TargetId = "box", Kind = InteractionKind.LastSlide });
        var behaviours = sourceActions.BehavioursOf(source, [shape]);

        var (_, history, actions, target) = Deck();
        target.AddAnimation(new AnimationEffect { Id = target.Id + ".anim1", TargetId = "ghost" });
        var clone = Assert.Single(actions.AddClones(target, [shape], 0, 0, "deck.action.paste", [], behaviours));

        Assert.Contains(target.Animations, effect => effect.TargetId == clone.Id && effect.Id != target.Id + ".anim1");
        Assert.Equal(InteractionKind.LastSlide, target.Interaction(clone.Id)?.Kind);
        Assert.Single(source.Animations);

        history.Undo();
        Assert.Single(target.Animations);
        Assert.Empty(target.Interactions);
    }

    [Fact]
    public void Deleting_grouped_elements_drops_their_member_references_in_the_same_step()
    {
        var (_, history, actions, slide) = Deck();
        var a = Shape("a");
        var b = Shape("b");
        var c = Shape("c");
        slide.Add(a);
        slide.Add(b);
        slide.Add(c);
        slide.ReplaceGroups([new SlideGroup("inner", ["a", "b"]), new SlideGroup("outer", ["inner", "c"])]);

        actions.DeleteElements(slide, [a, b], "deck.action.delete");

        var outer = Assert.Single(slide.Groups);
        Assert.Equal(["c"], outer.Members);

        history.Undo();
        Assert.Equal(2, slide.Groups.Count);
        Assert.Equal(["a", "b"], slide.Groups[0].Members);
        Assert.False(history.CanUndo);
    }

    private (Presentation Deck, UndoStack History, DeckActions Actions, Slide Slide) Deck()
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var history = new UndoStack();
        var actions = new DeckActions(deck, history, _factory, new FakeTextCatalog());
        var slide = _factory.Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        return (deck, history, actions, slide);
    }

    private static ObjectElement Shape(string id) => new()
    {
        Id = id,
        Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = id, Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle },
    };
}

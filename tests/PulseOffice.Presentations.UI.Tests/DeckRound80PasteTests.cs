using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckRound80PasteTests
{
    private readonly ISlideFactory _factory = new SlideFactory();

    [Fact]
    public void Pasting_into_another_deck_embeds_the_sounds_under_free_paths_and_drops_go_to_slide()
    {
        var (sourceDeck, _, sourceActions, source) = Deck();
        sourceDeck.Sounds.Add(new EmbeddedSound("Media/sound1.wav", "audio/wav", [1, 2, 3]));
        var other = _factory.Create(sourceDeck.NextId(), sourceDeck.Master!, SlideLayout.Blank);
        sourceDeck.AddExisting(other);
        source.Add(Shape("box"));
        source.Add(Shape("link"));
        source.AddAnimation(new AnimationEffect { Id = "a1", TargetId = "box", SoundPath = "Media/sound1.wav" });
        source.SetInteraction(new InteractiveAction { TargetId = "box", Kind = InteractionKind.PlaySound, SoundPath = "Media/sound1.wav" });
        source.SetInteraction(new InteractiveAction { TargetId = "link", Kind = InteractionKind.GoToSlide, Reference = other.Id });
        var selection = source.Elements.ToList();
        var behaviours = sourceActions.BehavioursOf(source, selection);

        var (targetDeck, history, actions, target) = Deck();
        targetDeck.Sounds.Add(new EmbeddedSound("Media/sound1.wav", "audio/wav", [9, 9]));
        var clones = actions.AddClones(target, selection, 0, 0, "deck.action.paste", [], behaviours);

        var box = clones.Single(clone => clone.ZOrder == clones.Min(c => c.ZOrder));
        var effect = Assert.Single(target.Animations);
        Assert.NotEqual("Media/sound1.wav", effect.SoundPath);
        Assert.Equal([1, 2, 3], targetDeck.Sounds.Find(effect.SoundPath)!.Data);
        Assert.Equal(effect.SoundPath, target.Interaction(box.Id)?.SoundPath);
        Assert.Equal([9, 9], targetDeck.Sounds.Find("Media/sound1.wav")!.Data);
        Assert.DoesNotContain(target.Interactions, action => action.Kind == InteractionKind.GoToSlide);

        history.Undo();
        Assert.Null(targetDeck.Sounds.Find(effect.SoundPath));
        history.Redo();
        Assert.NotNull(targetDeck.Sounds.Find(effect.SoundPath));
    }

    [Fact]
    public void Pasting_into_another_deck_reuses_an_identical_sound_and_the_same_deck_keeps_go_to_slide()
    {
        var (sourceDeck, _, sourceActions, source) = Deck();
        sourceDeck.Sounds.Add(new EmbeddedSound("Media/sound1.wav", "audio/wav", [4, 5]));
        source.Add(Shape("box"));
        source.SetInteraction(new InteractiveAction { TargetId = "box", Kind = InteractionKind.GoToSlide, Reference = source.Id, SoundPath = "Media/sound1.wav" });
        var behaviours = sourceActions.BehavioursOf(source, [.. source.Elements]);

        var (targetDeck, _, actions, target) = Deck();
        targetDeck.Sounds.Add(new EmbeddedSound("Media/sound7.wav", "audio/wav", [4, 5]));
        var shape = Assert.Single(source.Elements);
        actions.AddClones(target, [shape], 0, 0, "deck.action.paste", [], behaviours with { Interactions = [new InteractiveAction { TargetId = "box", Kind = InteractionKind.PlaySound, SoundPath = "Media/sound1.wav" }] });
        Assert.Equal("Media/sound7.wav", Assert.Single(target.Interactions).SoundPath);
        Assert.Single(targetDeck.Sounds.All);

        var duplicate = Assert.Single(sourceActions.AddClones(source, [shape], 200, 200, "deck.action.duplicateObject", [], behaviours));
        Assert.Equal(InteractionKind.GoToSlide, source.Interaction(duplicate.Id)?.Kind);
        Assert.Single(sourceDeck.Sounds.All);
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
        ZOrder = id == "box" ? 1 : 2,
        Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = id, Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle },
    };
}

using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckRound80MotionTests
{
    [Fact]
    public void Animation_target_names_are_localized_and_never_a_type_name()
    {
        var texts = new FakeTextCatalog();
        texts.Add("deck.placeholder.free", "Oggetto");
        var slide = new Slide("s1", SlideLayout.Blank);
        slide.Add(new ObjectElement { Id = "box", Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "box", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle } });

        Assert.Equal("Oggetto", new PulseOffice.Presentations.UI.Motion.DeckElementNames(texts).Of(slide, "box"));
    }

    [Fact]
    public void A_hyperlink_action_without_an_address_is_not_created_and_clearing_it_removes_it()
    {
        var form = new DeckInteractionForm();
        var current = new InteractiveAction { TargetId = "box", Kind = InteractionKind.Hyperlink, Reference = "https://example.org" };

        Assert.Null(form.Build("box", new DeckInteractionAnswer(InteractionKind.Hyperlink, "   "), null));
        Assert.Null(form.Build("box", new DeckInteractionAnswer(InteractionKind.Hyperlink, string.Empty), current));
        Assert.Equal("https://example.org/x", form.Build("box", new DeckInteractionAnswer(InteractionKind.Hyperlink, " https://example.org/x "), current)?.Reference);
        Assert.Equal(InteractionKind.NextSlide, form.Build("box", new DeckInteractionAnswer(InteractionKind.NextSlide, null), current)?.Kind);
    }

    [Fact]
    public void Choosing_another_preset_drops_the_old_direction_and_path()
    {
        var effect = new AnimationEffect { Id = "a", TargetId = "t", Category = AnimationCategory.Entrance, Preset = "fly-in", Subtype = "from-left", MotionPath = "M 0 0 L 1 1", DurationMs = 900 };
        var ordering = new AnimationOrdering();

        var changed = ordering.Rechosen(effect, AnimationCategory.MotionPath, "line");

        Assert.Null(changed.Subtype);
        Assert.Null(changed.MotionPath);
        Assert.Equal(900, changed.DurationMs);
        Assert.Same(effect, ordering.Rechosen(effect, AnimationCategory.Entrance, "fly-in"));
    }
}

using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round292IteratedMotionTests
{
    private readonly ITextIterationReveal _reveal = new TextIterationReveal();
    private readonly Theme _theme = new() { Name = "t" };

    private static AnimationEffect Effect(string preset) => new()
    {
        Id = "e1",
        TargetId = "t1",
        Category = AnimationCategory.Entrance,
        Preset = preset,
        Trigger = AnimationTrigger.WithPrevious,
        DurationMs = 1000,
        Native = new NativeAnimation(NativeAnimation.Ooxml, "<x/>", AnimationCategory.Entrance, preset, null, 1000, 1, null,
            new AnimationBehaviours { IterateType = "by-word", IterateInterval = 0.5 }),
    };

    private static TextElement Text(string text) => new() { Id = "t1", Text = new RichText(PlaceholderRole.Body, [new TextParagraph(text)]) };

    private static Slide SlideWith(SlideElement element, AnimationEffect effect)
    {
        var slide = new Slide("s", SlideLayout.Blank);
        slide.Add(element);
        slide.AddAnimation(effect);
        return slide;
    }

    private static TextReveal Moving(int elapsed) =>
        new(TextIterationUnit.Word, false, false, elapsed, 500, 1000, progress => progress, progress => new ElementVisual(progress, (1 - progress) * 10, 0, 1));

    [Fact]
    public void An_iterated_fly_carries_the_unit_motion_and_a_fade_does_not()
    {
        var scheduler = new AnimationScheduler();
        var fly = scheduler.Schedule(SlideWith(Text("a b"), Effect("fly-in")));
        var fade = scheduler.Schedule(SlideWith(Text("a b"), Effect("fade")));

        Assert.NotNull(scheduler.VisualAt("t1", fly, fly[0].StartMs + 250).Reveal.Motion);
        Assert.Null(scheduler.VisualAt("t1", fade, fade[0].StartMs + 250).Reveal.Motion);
    }

    [Fact]
    public void Units_in_flight_leave_the_still_pass_and_move_in_their_own_layers()
    {
        var reveal = Moving(750);

        var still = _reveal.Opacities("a b c", reveal);
        var layers = _reveal.Layers(Text("a b c"), reveal);

        Assert.All(still, opacity => Assert.Equal(0, opacity));
        Assert.Equal(2, layers.Count);
        Assert.Equal(7.5, layers[0].Visual.OffsetX, 6);
        Assert.Equal(2.5, layers[1].Visual.OffsetX, 6);
        Assert.Equal(new double[] { 0, 0, 1, 0, 0 }, _reveal.Opacities("a b c", layers[0].Reveal));
    }

    [Fact]
    public void A_settled_unit_stays_in_the_still_pass()
    {
        var still = _reveal.Opacities("a b", Moving(1200));

        Assert.Equal(1, still[0]);
        Assert.Equal(0, still[2]);
    }

    [Fact]
    public void A_layered_text_element_drops_its_backdrop()
    {
        var element = Text("a b") with { Backdrop = new TextBackdrop(ShapeKind.Rectangle, ShapeFill.Default, ShapeLine.Default) };
        var layer = _reveal.Layers(element, Moving(250))[0];

        var layered = Assert.IsType<TextElement>(_reveal.Layered(element, layer.Reveal, _theme));

        Assert.Null(layered.Backdrop);
    }

    [Fact]
    public void A_layered_shape_draws_only_its_text_in_the_resolved_ink()
    {
        var element = new ObjectElement { Id = "t1", Content = new ShapeObject { Id = "sh", Shape = ShapeKind.Rectangle, Text = "a b" } };
        var layer = _reveal.Layers(element, Moving(250))[0];

        var shape = Assert.IsType<ShapeObject>(Assert.IsType<ObjectElement>(_reveal.Layered(element, layer.Reveal, _theme, "#123456")).Content);

        Assert.False(shape.Style.Fill.IsVisible);
        Assert.Equal("#123456", shape.Style.Text.ColorHex);
        Assert.False(shape.Style.Text.AutomaticColor);
    }

    [Fact]
    public void A_fading_shape_unit_without_its_own_colour_takes_the_given_ink()
    {
        var shape = new ShapeObject
        {
            Id = "sh",
            Shape = ShapeKind.Rectangle,
            Text = "ab",
            Style = new ShapeStyle { Text = new ShapeTextStyle { ColorHex = "#FFFFFF", AutomaticColor = true } },
        };

        var revealed = _reveal.Reveal(shape, new TextReveal(TextIterationUnit.Word, false, false, 500, 100, 1000, progress => progress), "#000000");

        Assert.EndsWith("000000", revealed.Runs![0].ColorHex);
    }

    [Fact]
    public void The_timeline_of_a_slide_shows_the_whole_iterated_span()
    {
        var timeline = new AnimationTimeline(new AnimationScheduler());

        var view = timeline.Build(SlideWith(Text("a b c"), Effect("fade")));

        Assert.Equal(2000, view.Rows[0].EndMs - view.Rows[0].StartMs);
    }
}

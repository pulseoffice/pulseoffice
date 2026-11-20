using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round286IteratedEffectTests
{
    private readonly ITextIterationReveal _reveal = new TextIterationReveal();
    private readonly Theme _theme = new() { Name = "t" };

    private AnimationEffect Effect(string preset, string target = "t1") => new()
    {
        Id = "e1",
        TargetId = target,
        Trigger = AnimationTrigger.WithPrevious,
        DurationMs = 1000,
        Native = new NativeAnimation(NativeAnimation.Ooxml, "<x/>", AnimationCategory.Entrance, preset, null, 1000, 1, null,
            new AnimationBehaviours { IterateType = "by-word", IterateInterval = 0.5 }),
    };

    private Slide SlideWith(SlideElement element, AnimationEffect effect)
    {
        var slide = new Slide("s", SlideLayout.Blank);
        slide.Add(element);
        slide.AddAnimation(effect);
        return slide;
    }

    [Fact]
    public void TheIterationDelaySpreadsUnitsAndEachUnitPlaysTheWholeEffect()
    {
        var text = new TextElement { Id = "t1", Text = new RichText(PlaceholderRole.Body, [new TextParagraph("a b c")]) };
        var scheduler = new AnimationScheduler();

        var schedule = scheduler.Schedule(SlideWith(text, Effect("fade")));

        Assert.Equal(1000 + 2 * 500, schedule[0].EndMs - schedule[0].StartMs);
    }

    [Fact]
    public void EachUnitFadesInOverTheEffectDuration()
    {
        var reveal = new TextReveal(TextIterationUnit.Word, false, false, 750, 500, 1000, progress => progress);

        var opacities = _reveal.Opacities("a b c", reveal);

        Assert.Equal(0.75, opacities[0], 3);
        Assert.Equal(0.25, opacities[2], 3);
        Assert.Equal(0, opacities[4]);
    }

    [Fact]
    public void AFadingRunTakesItsResolvedInkWithPartialAlpha()
    {
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph("ab") { Runs = [new TextRun("ab") { Color = 0xFF102030u }] }]);

        var revealed = _reveal.Reveal(text, new TextReveal(TextIterationUnit.Word, false, false, 500, 100, 1000, progress => progress), _theme);

        var colour = revealed.Paragraphs[0].Runs[0].Color!.Value;
        Assert.Equal(0x102030u, colour & 0xFFFFFFu);
        Assert.InRange(colour >> 24, 120u, 135u);
    }

    [Fact]
    public void AnUnrevealedParagraphHidesItsPictureBullet()
    {
        var picture = new ListBulletPicture(new byte[] { 1 }, "image/png", 10, 10);
        var text = new RichText(PlaceholderRole.Body,
        [
            new TextParagraph("one"),
            new TextParagraph("two") { Bullet = true, BulletSymbol = new BulletSymbol("•") { Picture = picture } },
        ]);

        var revealed = _reveal.Reveal(text, new TextReveal(TextIterationUnit.Word, false, false, 0, 1000), _theme);

        Assert.True(revealed.Paragraphs[1].BulletSymbol!.Transparent);
        Assert.Same(picture, revealed.Paragraphs[1].BulletSymbol!.Picture);
        Assert.False(revealed.Paragraphs[0].BulletSymbol?.Transparent ?? false);
    }

    [Fact]
    public void ShapeTextIsRevealedUnitByUnit()
    {
        var shape = new ShapeObject { Id = "sh", Shape = ShapeKind.Rectangle, Text = "ab cd" };

        var revealed = _reveal.Reveal(shape, new TextReveal(TextIterationUnit.Word, false, false, 0, 1000));

        Assert.Equal("ab cd", string.Concat(revealed.Runs!.Select(run => run.Text)));
        Assert.Null(revealed.Runs![0].ColorHex);
        Assert.Equal("#00", revealed.Runs![^1].ColorHex![..3]);
    }

    [Fact]
    public void AnIteratedShapeEffectIsScheduledAndRevealed()
    {
        var element = new ObjectElement { Id = "t1", Content = new ShapeObject { Id = "sh", Shape = ShapeKind.Rectangle, Text = "x y" } };
        var scheduler = new AnimationScheduler();
        var schedule = scheduler.Schedule(SlideWith(element, Effect("fade")));

        var visual = scheduler.VisualAt("t1", schedule, schedule[0].StartMs + 100);

        Assert.Equal(1500, schedule[0].EndMs - schedule[0].StartMs);
        Assert.Equal(TextIterationUnit.Word, visual.Reveal.Unit);
        Assert.NotNull(visual.Reveal.Opacity);
    }
}

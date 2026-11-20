using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round283IteratedTextTests
{
    private readonly ITextIterationReveal _reveal = new TextIterationReveal();

    private string Shown(string text, TextReveal reveal)
    {
        var mask = _reveal.Visible(text, reveal);
        return new string([.. text.Select((c, i) => mask[i] ? c : '_')]);
    }

    [Fact]
    public void ByWordRevealsOneWordPerInterval()
    {
        var reveal = new TextReveal(TextIterationUnit.Word, false, false, 150, 100);

        Assert.Equal("one_two______", Shown("one two three", reveal));
    }

    [Fact]
    public void ByLetterBackwardsStartsFromTheEnd()
    {
        var reveal = new TextReveal(TextIterationUnit.Letter, true, false, 0, 100);

        Assert.Equal("___d", Shown("abcd", reveal));
    }

    [Fact]
    public void AnExitHidesUnitsInOrderAndTheEndShowsEverything()
    {
        Assert.Equal("___ b", Shown("aaa b", new TextReveal(TextIterationUnit.Word, false, true, 0, 1000)));
        Assert.Equal("aaa_b", Shown("aaa b", new TextReveal(TextIterationUnit.Word, false, false, 1000, 1000)));
    }

    [Fact]
    public void HiddenRunsKeepTheirTextAndBecomeTransparent()
    {
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph("ab cd") { Runs = [new TextRun("ab cd") { Color = 0xFF112233u }] }]);

        var revealed = _reveal.Reveal(text, new TextReveal(TextIterationUnit.Word, false, false, 0, 100), new Theme { Name = "t" });

        var runs = revealed.Paragraphs[0].Runs;
        Assert.Equal("ab cd", string.Concat(runs.Select(run => run.Text)));
        Assert.Equal(0xFF112233u, runs[0].Color);
        Assert.Equal(0u, runs[^1].Color);
    }

    [Fact]
    public void TheSchedulerRevealsIteratedEntrancesProgressively()
    {
        var behaviours = new AnimationBehaviours { IterateType = "by-word", IterateInterval = 0.1 };
        var effect = new AnimationEffect
        {
            Id = "e1",
            TargetId = "t1",
            Trigger = AnimationTrigger.WithPrevious,
            DurationMs = 1000,
            Native = new NativeAnimation(NativeAnimation.Ooxml, "<x/>", AnimationCategory.Entrance, "fade", null, 1000, 1, null, behaviours),
        };
        var scheduler = new AnimationScheduler();
        var schedule = scheduler.Schedule([effect]);

        var during = scheduler.VisualAt("t1", schedule, schedule[0].StartMs + 150);
        var after = scheduler.VisualAt("t1", schedule, schedule[0].EndMs);

        Assert.Equal(TextIterationUnit.Word, during.Reveal.Unit);
        Assert.Equal(1.0, during.Opacity);
        Assert.Equal(150, during.Reveal.ElapsedMs);
        Assert.True(after.Reveal.IsWhole);
    }
}

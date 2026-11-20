using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round283TransitionSoundCueTests
{
    private readonly ITransitionSoundCues _cues = new TransitionSoundCues();

    [Fact]
    public void ASoundCutAtTheNextSlideStopsOnArrival()
    {
        var cut = new SlideTransition { SoundPath = "a.wav", SoundPlaysToEnd = false };

        Assert.Equal(TransitionSoundAction.Stop, _cues.Arriving(cut, SlideTransition.None).Action);
        Assert.Equal(TransitionSoundAction.Keep, _cues.Arriving(cut with { SoundPlaysToEnd = true }, SlideTransition.None).Action);
        Assert.Equal(TransitionSoundAction.Keep, _cues.Arriving(cut with { LoopSoundUntilNext = true }, SlideTransition.None).Action);
    }

    [Fact]
    public void TheArrivingSlideDecidesFirst()
    {
        var played = _cues.Arriving(null, new SlideTransition { SoundPath = "b.wav", LoopSoundUntilNext = true });

        Assert.Equal(new TransitionSoundCue(TransitionSoundAction.Play, "b.wav", true), played);
        Assert.Equal(TransitionSoundAction.Stop, _cues.Arriving(null, new SlideTransition { StopPreviousSound = true }).Action);
    }
}

public sealed class Round283PauseCurtainTests
{
    [Fact]
    public void ThePauseScreenIsNotABlankedScreenAndResumes()
    {
        var keypad = new ShowKeypad();
        keypad.Pause();

        Assert.Equal(ShowCurtain.Pause, keypad.Screen);
        Assert.False(keypad.IsBlanked);
        keypad.Resume();
        Assert.Equal(ShowCurtain.Slide, keypad.Screen);
    }
}

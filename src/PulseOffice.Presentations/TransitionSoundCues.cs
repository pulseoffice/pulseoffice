namespace PulseOffice.Presentations;

public enum TransitionSoundAction
{
    Keep,
    Play,
    Stop,
}

public sealed record TransitionSoundCue(TransitionSoundAction Action, string? Path = null, bool Loop = false)
{
    public static readonly TransitionSoundCue Keep = new(TransitionSoundAction.Keep);

    public static readonly TransitionSoundCue Stop = new(TransitionSoundAction.Stop);
}

public interface ITransitionSoundCues
{
    TransitionSoundCue Arriving(SlideTransition? left, SlideTransition? arriving);
}

public sealed class TransitionSoundCues : ITransitionSoundCues
{
    public TransitionSoundCue Arriving(SlideTransition? left, SlideTransition? arriving)
    {
        if (arriving is { SoundPath: { Length: > 0 } path }) return new TransitionSoundCue(TransitionSoundAction.Play, path, arriving.LoopSoundUntilNext);
        if (arriving is { StopPreviousSound: true }) return TransitionSoundCue.Stop;
        return left is { SoundPath.Length: > 0, SoundPlaysToEnd: false, LoopSoundUntilNext: false } ? TransitionSoundCue.Stop : TransitionSoundCue.Keep;
    }
}

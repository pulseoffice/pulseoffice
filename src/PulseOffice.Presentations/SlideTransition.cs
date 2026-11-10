namespace PulseOffice.Presentations;

public enum TransitionKind
{
    None,
    Fade,
    Push,
    Wipe,
    Split,
    Reveal,
    Cover,
    Uncover,
    Cut,
    Dissolve,
    Morph,
    Zoom,
}

public enum TransitionDirection
{
    None,
    Left,
    Right,
    Up,
    Down,
    LeftDown,
    LeftUp,
    RightDown,
    RightUp,
    In,
    Out,
    Horizontal,
    Vertical,
    Clockwise,
    CounterClockwise,
}

public enum AdvanceMode
{
    OnClick,
    AfterTime,
    OnClickOrAfterTime,
}

public sealed record SlideTransition
{
    public static readonly SlideTransition None = new();

    public TransitionKind Kind { get; init; } = TransitionKind.None;

    public TransitionDirection Direction { get; init; } = TransitionDirection.None;

    public int DurationMs { get; init; } = 500;

    public AdvanceMode Advance { get; init; } = AdvanceMode.OnClick;

    public int AdvanceAfterMs { get; init; }

    public string? SoundPath { get; init; }

    public bool LoopSoundUntilNext { get; init; }

    public NativeTransition? Native { get; init; }

    public NativeTransition? CurrentNative => Native is { } native && native.Describes(this) ? native : null;

    public bool AdvancesAfterTime => Advance is AdvanceMode.AfterTime or AdvanceMode.OnClickOrAfterTime;

    public bool IsNone => Kind == TransitionKind.None;

    public bool IsEmpty => IsNone && Advance == AdvanceMode.OnClick && string.IsNullOrEmpty(SoundPath);
}

public sealed record NativeTransition(string Format, string Payload, TransitionKind Kind, TransitionDirection Direction)
{
    public const string Odf = "odf";

    public const string Ooxml = "ooxml";

    public bool Describes(SlideTransition transition) => transition.Kind == Kind && transition.Direction == Direction;
}

public sealed record SmilTransition(string Type, string Subtype, bool Reverse);

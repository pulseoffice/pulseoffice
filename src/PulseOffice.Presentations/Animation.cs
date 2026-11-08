namespace PulseOffice.Presentations;

public enum AnimationCategory
{
    Entrance,
    Emphasis,
    Exit,
    MotionPath,
    Media,
}

public enum AnimationTrigger
{
    OnClick,
    WithPrevious,
    AfterPrevious,
}

public sealed record AnimationEffect
{
    public required string Id { get; init; }

    public required string TargetId { get; init; }

    public int? Paragraph { get; init; }

    public AnimationCategory Category { get; init; } = AnimationCategory.Entrance;

    public string Preset { get; init; } = "fade";

    public AnimationTrigger Trigger { get; init; } = AnimationTrigger.OnClick;

    public int DelayMs { get; init; }

    public int DurationMs { get; init; } = 500;

    public int Repeat { get; init; } = 1;

    public string? MotionPath { get; init; }

    public string? SoundPath { get; init; }

    public string? Subtype { get; init; }

    public NativeAnimation? Native { get; init; }

    public NativeAnimation? CurrentNative => Native is { } native && native.Describes(this) ? native : null;
}

public sealed record NativeAnimation(string Format, string Xml, AnimationCategory Category, string Preset, string? Subtype, int DurationMs, int Repeat, string? MotionPath, AnimationBehaviours? Behaviours = null)
{
    public const string Odf = "odf";

    public const string Ooxml = "ooxml";

    public bool Describes(AnimationEffect effect) =>
        effect.Category == Category && effect.Preset == Preset && effect.Subtype == Subtype
        && effect.DurationMs == DurationMs && effect.Repeat == Repeat && effect.MotionPath == MotionPath;
}

public sealed record InteractiveAction
{
    public required string TargetId { get; init; }

    public InteractionKind Kind { get; init; } = InteractionKind.None;

    public string? Reference { get; init; }

    public string? SoundPath { get; init; }
}

public enum InteractionKind
{
    None,
    NextSlide,
    PreviousSlide,
    FirstSlide,
    LastSlide,
    GoToSlide,
    Hyperlink,
    RunMacro,
    PlaySound,
    StopShow,
}

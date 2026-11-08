namespace PulseOffice.Presentations;

public enum TransitionGroup
{
    Subtle,
    Exciting,
    Dynamic,
}

public sealed record TransitionCatalogEntry(TransitionKind Kind, TransitionGroup Group, IReadOnlyList<TransitionDirection> Directions)
{
    public TransitionDirection DefaultDirection => Directions.Count > 0 ? Directions[0] : TransitionDirection.None;
}

public interface ITransitionCatalog
{
    IReadOnlyList<TransitionCatalogEntry> Entries { get; }

    TransitionCatalogEntry Find(TransitionKind kind);

    SlideTransition Choose(SlideTransition current, TransitionKind kind);

    IReadOnlyList<string> SoundsIn(Presentation deck);
}

public sealed class TransitionCatalog : ITransitionCatalog
{
    private static readonly IReadOnlyList<TransitionDirection> Sides =
        [TransitionDirection.Left, TransitionDirection.Right, TransitionDirection.Up, TransitionDirection.Down];

    public IReadOnlyList<TransitionCatalogEntry> Entries { get; } =
    [
        new(TransitionKind.None, TransitionGroup.Subtle, []),
        new(TransitionKind.Morph, TransitionGroup.Subtle, []),
        new(TransitionKind.Fade, TransitionGroup.Subtle, []),
        new(TransitionKind.Push, TransitionGroup.Subtle, Sides),
        new(TransitionKind.Wipe, TransitionGroup.Subtle, Sides),
        new(TransitionKind.Split, TransitionGroup.Subtle, []),
        new(TransitionKind.Reveal, TransitionGroup.Subtle, []),
        new(TransitionKind.Cut, TransitionGroup.Subtle, []),
        new(TransitionKind.Cover, TransitionGroup.Exciting, Sides),
        new(TransitionKind.Uncover, TransitionGroup.Exciting, Sides),
        new(TransitionKind.Dissolve, TransitionGroup.Exciting, []),
        new(TransitionKind.Zoom, TransitionGroup.Dynamic, []),
    ];

    public TransitionCatalogEntry Find(TransitionKind kind) =>
        Entries.FirstOrDefault(entry => entry.Kind == kind) ?? new TransitionCatalogEntry(kind, TransitionGroup.Subtle, []);

    public SlideTransition Choose(SlideTransition current, TransitionKind kind)
    {
        ArgumentNullException.ThrowIfNull(current);
        var entry = Find(kind);
        var direction = entry.Directions.Contains(current.Direction) ? current.Direction : entry.DefaultDirection;
        return current with { Kind = kind, Direction = direction };
    }

    public IReadOnlyList<string> SoundsIn(Presentation deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        return [.. deck.Slides
            .Select(slide => slide.Transition.SoundPath)
            .OfType<string>()
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.OrdinalIgnoreCase)];
    }
}

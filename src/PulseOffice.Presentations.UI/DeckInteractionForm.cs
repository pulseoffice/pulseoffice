namespace PulseOffice.Presentations.UI;

public sealed record DeckInteractionChoices(IReadOnlyList<InteractionKind> Kinds, int Kind, int Slide, string Reference);

public sealed record DeckInteractionAnswer(InteractionKind Kind, string? Reference);

public interface IDeckInteractionForm
{
    DeckInteractionChoices Open(InteractiveAction? current, IReadOnlyList<Slide> slides);

    string LabelKey(InteractionKind kind);

    DeckInteractionAnswer Read(DeckInteractionChoices choices, IReadOnlyDictionary<string, object> values, IReadOnlyList<Slide> slides);

    InteractiveAction? Build(string targetId, DeckInteractionAnswer answer, InteractiveAction? current);
}

public sealed class DeckInteractionForm : IDeckInteractionForm
{
    public const string KindKey = "kind";
    public const string SlideKey = "slide";
    public const string ReferenceKey = "reference";

    private readonly InteractionKind[] _kinds =
    [
        InteractionKind.None, InteractionKind.NextSlide, InteractionKind.PreviousSlide, InteractionKind.FirstSlide,
        InteractionKind.LastSlide, InteractionKind.GoToSlide, InteractionKind.Hyperlink, InteractionKind.RunMacro,
        InteractionKind.StopShow,
    ];

    public DeckInteractionChoices Open(InteractiveAction? current, IReadOnlyList<Slide> slides)
    {
        var kinds = _kinds.ToList();
        if (current is { SoundPath: { Length: > 0 } } && !kinds.Contains(InteractionKind.PlaySound)) kinds.Add(InteractionKind.PlaySound);
        if (current is { } existing && !kinds.Contains(existing.Kind)) kinds.Add(existing.Kind);
        var slide = current is { Kind: InteractionKind.GoToSlide, Reference: { } id } ? Math.Max(0, slides.ToList().FindIndex(s => s.Id == id)) : 0;
        var reference = current is { Kind: InteractionKind.Hyperlink } ? current.Reference ?? string.Empty : string.Empty;
        return new DeckInteractionChoices(kinds, Math.Max(0, kinds.IndexOf(current?.Kind ?? InteractionKind.None)), slide, reference);
    }

    public string LabelKey(InteractionKind kind) =>
        $"deck.interactionDialog.{char.ToLowerInvariant(kind.ToString()[0])}{kind.ToString()[1..]}";

    public DeckInteractionAnswer Read(DeckInteractionChoices choices, IReadOnlyDictionary<string, object> values, IReadOnlyList<Slide> slides)
    {
        var index = values.TryGetValue(KindKey, out var chosen) && chosen is int k && k >= 0 && k < choices.Kinds.Count ? k : 0;
        var kind = choices.Kinds[index];
        var reference = kind switch
        {
            InteractionKind.GoToSlide when slides.Count > 0 =>
                slides[values.TryGetValue(SlideKey, out var slide) && slide is int s && s >= 0 && s < slides.Count ? s : 0].Id,
            InteractionKind.Hyperlink => (values.TryGetValue(ReferenceKey, out var text) ? text as string : null)?.Trim(),
            _ => null,
        };
        return new DeckInteractionAnswer(kind, reference);
    }

    public InteractiveAction? Build(string targetId, DeckInteractionAnswer answer, InteractiveAction? current)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var reference = answer.Kind == InteractionKind.Hyperlink ? answer.Reference?.Trim() : answer.Reference ?? (current?.Kind == answer.Kind ? current.Reference : null);
        if (answer.Kind == InteractionKind.None || (answer.Kind is InteractionKind.Hyperlink or InteractionKind.GoToSlide && string.IsNullOrWhiteSpace(reference))) return null;
        return new InteractiveAction { TargetId = targetId, Kind = answer.Kind, Reference = reference, SoundPath = current?.SoundPath };
    }
}

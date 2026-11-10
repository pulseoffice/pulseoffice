namespace PulseOffice.Presentations;

public sealed record ElementBehaviourSet(IReadOnlyList<AnimationEffect> Animations, IReadOnlyList<InteractiveAction> Interactions)
{
    public Presentation? Origin { get; init; }

    public IReadOnlyList<EmbeddedSound> Sounds { get; init; } = [];

    public bool IsEmpty => Animations.Count == 0 && Interactions.Count == 0;
}

public interface IElementBehaviourCloning
{
    ElementBehaviourSet Of(Presentation deck, Slide slide, IEnumerable<string> elementIds);

    ElementBehaviourSet Cloned(Presentation targetDeck, Slide target, ElementBehaviourSet source, IReadOnlyDictionary<string, string> elementIds);
}

public sealed class ElementBehaviourCloning(SoundTypes soundTypes) : IElementBehaviourCloning
{
    public ElementBehaviourCloning() : this(new SoundTypes())
    {
    }

    public ElementBehaviourSet Of(Presentation deck, Slide slide, IEnumerable<string> elementIds)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(elementIds);
        var ids = elementIds.ToHashSet(StringComparer.Ordinal);
        List<AnimationEffect> animations = [.. slide.Animations.Where(effect => ids.Contains(effect.TargetId))];
        List<InteractiveAction> interactions = [.. slide.Interactions.Where(action => ids.Contains(action.TargetId))];
        var sounds = animations.Select(effect => effect.SoundPath).Concat(interactions.Select(action => action.SoundPath))
            .Distinct(StringComparer.Ordinal).Select(deck.Sounds.Find).OfType<EmbeddedSound>().ToList();
        return new ElementBehaviourSet(animations, interactions) { Origin = deck, Sounds = sounds };
    }

    public ElementBehaviourSet Cloned(Presentation targetDeck, Slide target, ElementBehaviourSet source, IReadOnlyDictionary<string, string> elementIds)
    {
        ArgumentNullException.ThrowIfNull(targetDeck);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(elementIds);
        var foreign = !ReferenceEquals(source.Origin, targetDeck);
        var sounds = foreign ? SoundImport(targetDeck, source.Sounds) : new SoundImportPlan();
        var taken = target.Animations.Select(effect => effect.Id).ToHashSet(StringComparer.Ordinal);
        var animations = new List<AnimationEffect>();
        foreach (var effect in source.Animations)
            if (elementIds.TryGetValue(effect.TargetId, out var mapped))
                animations.Add(effect with { Id = FreeId(target.Id, taken), TargetId = mapped, SoundPath = sounds.Mapped(effect.SoundPath),
                    TriggerShapeId = effect.TriggerShapeId is { } trigger && elementIds.TryGetValue(trigger, out var mappedTrigger) ? mappedTrigger : null });
        var interactions = source.Interactions
            .Where(action => elementIds.ContainsKey(action.TargetId))
            .Where(action => !foreign || action.Kind != InteractionKind.GoToSlide)
            .GroupBy(action => elementIds[action.TargetId], StringComparer.Ordinal)
            .Select(group => group.First() with { TargetId = group.Key, SoundPath = sounds.Mapped(group.First().SoundPath) })
            .ToList();
        return new ElementBehaviourSet(animations, interactions) { Origin = targetDeck, Sounds = sounds.Added };
    }

    private sealed class SoundImportPlan
    {
        public Dictionary<string, string> Paths { get; } = new(StringComparer.Ordinal);

        public List<EmbeddedSound> Added { get; } = [];

        public string? Mapped(string? path) => path is not null && Paths.TryGetValue(path, out var mapped) ? mapped : path;
    }

    private SoundImportPlan SoundImport(Presentation targetDeck, IReadOnlyList<EmbeddedSound> sounds)
    {
        var plan = new SoundImportPlan();
        foreach (var sound in sounds)
        {
            var existing = targetDeck.Sounds.All.Concat(plan.Added).FirstOrDefault(candidate => candidate.Data.AsSpan().SequenceEqual(sound.Data));
            if (existing is not null)
            {
                plan.Paths[sound.Path] = existing.Path;
                continue;
            }
            var path = FreePath(targetDeck, plan.Added, soundTypes.ExtensionOf(sound.MimeType));
            plan.Added.Add(sound with { Path = path });
            plan.Paths[sound.Path] = path;
        }
        return plan;
    }

    private string FreePath(Presentation targetDeck, List<EmbeddedSound> added, string extension)
    {
        var number = targetDeck.Sounds.All.Count + added.Count;
        string candidate;
        do candidate = $"Media/sound{++number}.{extension}";
        while (targetDeck.Sounds.Find(candidate) is not null || added.Any(sound => sound.Path == candidate));
        return candidate;
    }

    private string FreeId(string slideId, HashSet<string> taken)
    {
        var index = taken.Count;
        string candidate;
        do candidate = $"{slideId}.anim{++index}";
        while (!taken.Add(candidate));
        return candidate;
    }
}

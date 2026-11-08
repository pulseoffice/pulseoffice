namespace PulseOffice.Presentations;

public interface IAnimationPresetRegistry
{
    string OoxmlPresetId(AnimationCategory category, string preset);

    string OdfPresetId(AnimationCategory category, string preset);

    string FromOoxml(AnimationCategory category, string presetId);

    string FromOdf(AnimationCategory category, string presetId);

    IReadOnlyList<string> Presets(AnimationCategory category);

    bool ReachesOoxml(AnimationCategory category, string preset);
}

public sealed class AnimationPresetRegistry(INativeAnimationPresetTable natives) : IAnimationPresetRegistry
{
    public AnimationPresetRegistry() : this(new NativeAnimationPresetTable())
    {
    }

    private sealed record Entry(AnimationCategory Category, string Canonical, int Ooxml, string Odf);

    private readonly IReadOnlyList<Entry> _entries =
    [
        new(AnimationCategory.Entrance, "appear", 1, "ooo-entrance-appear"),
        new(AnimationCategory.Entrance, "fly-in", 2, "ooo-entrance-fly-in"),
        new(AnimationCategory.Entrance, "blinds", 3, "ooo-entrance-venetian-blinds"),
        new(AnimationCategory.Entrance, "box", 4, "ooo-entrance-box"),
        new(AnimationCategory.Entrance, "checkerboard", 5, "ooo-entrance-checkerboard"),
        new(AnimationCategory.Entrance, "circle", 6, "ooo-entrance-circle"),
        new(AnimationCategory.Entrance, "crawl-in", 7, "ooo-entrance-fly-in-slow"),
        new(AnimationCategory.Entrance, "diamond", 8, "ooo-entrance-diamond"),
        new(AnimationCategory.Entrance, "dissolve-in", 9, "ooo-entrance-dissolve-in"),
        new(AnimationCategory.Entrance, "fade", 10, "ooo-entrance-fade-in"),
        new(AnimationCategory.Entrance, "flash-once", 11, "ooo-entrance-flash-once"),
        new(AnimationCategory.Entrance, "peek-in", 12, "ooo-entrance-peek-in"),
        new(AnimationCategory.Entrance, "plus", 13, "ooo-entrance-plus"),
        new(AnimationCategory.Entrance, "random-bars", 14, "ooo-entrance-random-bars"),
        new(AnimationCategory.Entrance, "spiral-in", 15, "ooo-entrance-spiral-in"),
        new(AnimationCategory.Entrance, "split", 16, "ooo-entrance-split"),
        new(AnimationCategory.Entrance, "stretchy", 17, "ooo-entrance-stretchy"),
        new(AnimationCategory.Entrance, "strips", 18, "ooo-entrance-diagonal-squares"),
        new(AnimationCategory.Entrance, "wedge", 20, "ooo-entrance-wedge"),
        new(AnimationCategory.Entrance, "wheel", 21, "ooo-entrance-wheel"),
        new(AnimationCategory.Entrance, "wipe", 22, "ooo-entrance-wipe"),
        new(AnimationCategory.Entrance, "zoom", 23, "ooo-entrance-zoom"),
        new(AnimationCategory.Entrance, "random", 24, "ooo-entrance-random"),
        new(AnimationCategory.Entrance, "bounce", 26, "ooo-entrance-bounce"),
        new(AnimationCategory.Entrance, "grow-and-turn", 31, "ooo-entrance-turn-and-grow"),
        new(AnimationCategory.Entrance, "swivel", 19, "ooo-entrance-swivel"),
        new(AnimationCategory.Entrance, "float", 30, "ooo-entrance-float"),

        new(AnimationCategory.Emphasis, "grow", 6, "ooo-emphasis-grow-and-shrink"),
        new(AnimationCategory.Emphasis, "spin", 8, "ooo-emphasis-spin"),
        new(AnimationCategory.Emphasis, "pulse", 26, "ooo-emphasis-flash-bulb"),
        new(AnimationCategory.Emphasis, "teeter", 32, "ooo-emphasis-teeter"),
        new(AnimationCategory.Emphasis, "flash", 27, "ooo-emphasis-flicker"),
        new(AnimationCategory.Emphasis, "color", 1, "ooo-emphasis-fill-color"),
        new(AnimationCategory.Emphasis, "transparency", 9, "ooo-emphasis-transparency"),
        new(AnimationCategory.Emphasis, "bold-flash", 10, "ooo-emphasis-bold-flash"),
        new(AnimationCategory.Emphasis, "wave", 34, "ooo-emphasis-wave"),
        new(AnimationCategory.Emphasis, "blink", 35, "ooo-emphasis-blink"),

        new(AnimationCategory.Exit, "disappear", 1, "ooo-exit-disappear"),
        new(AnimationCategory.Exit, "fly-out", 2, "ooo-exit-fly-out"),
        new(AnimationCategory.Exit, "box", 4, "ooo-exit-box"),
        new(AnimationCategory.Exit, "checkerboard", 5, "ooo-exit-checkerboard"),
        new(AnimationCategory.Exit, "circle", 6, "ooo-exit-circle"),
        new(AnimationCategory.Exit, "crawl-out", 7, "ooo-exit-crawl-out"),
        new(AnimationCategory.Exit, "diamond", 8, "ooo-exit-diamond"),
        new(AnimationCategory.Exit, "dissolve-out", 9, "ooo-exit-dissolve"),
        new(AnimationCategory.Exit, "fade", 10, "ooo-exit-fade-out"),
        new(AnimationCategory.Exit, "peek-out", 12, "ooo-exit-peek-out"),
        new(AnimationCategory.Exit, "random-bars", 14, "ooo-exit-random-bars"),
        new(AnimationCategory.Exit, "split", 16, "ooo-exit-split"),
        new(AnimationCategory.Exit, "wipe", 22, "ooo-exit-wipe"),
        new(AnimationCategory.Exit, "zoom", 23, "ooo-exit-zoom"),

        new(AnimationCategory.MotionPath, "line", 42, "ooo-motionpath-down"),
        new(AnimationCategory.MotionPath, "curve", 23, "ooo-motionpath-curvy-star"),

        new(AnimationCategory.Media, "play", 1, "play"),
        new(AnimationCategory.Media, "toggle-pause", 2, "toggle-pause"),
        new(AnimationCategory.Media, "stop", 3, "stop"),
    ];

    public IReadOnlyList<string> Presets(AnimationCategory category) =>
        [.. _entries.Where(entry => entry.Category == category).Select(entry => entry.Canonical)];

    public string OoxmlPresetId(AnimationCategory category, string preset)
    {
        if (Match(category, e => e.Canonical == preset) is { } entry) return entry.Ooxml.ToString();
        if (OoxmlNative(preset) is { } id) return id.ToString();
        if (natives.ByOdf(category, preset) is { } pair) return pair.Ooxml.ToString();
        return DefaultFor(category).Ooxml.ToString();
    }

    public string OdfPresetId(AnimationCategory category, string preset)
    {
        if (Match(category, e => e.Canonical == preset) is { } entry) return entry.Odf;
        if (OoxmlNative(preset) is { } id)
            return natives.ByOoxml(category, id)?.Odf ?? preset;
        return preset.Length > 0 ? preset : DefaultFor(category).Odf;
    }

    public string FromOoxml(AnimationCategory category, string presetId)
    {
        if (int.TryParse(presetId, out var id) && Match(category, e => e.Ooxml == id) is { } entry)
            return entry.Canonical;
        return $"ooxml-{presetId}";
    }

    public string FromOdf(AnimationCategory category, string presetId)
    {
        if (Match(category, e => e.Odf == presetId) is { } entry) return entry.Canonical;
        return presetId;
    }

    public bool ReachesOoxml(AnimationCategory category, string preset) =>
        Match(category, e => e.Canonical == preset) is not null
        || OoxmlNative(preset) is not null
        || natives.ByOdf(category, preset) is not null;

    private int? OoxmlNative(string preset) =>
        preset.StartsWith("ooxml-", StringComparison.Ordinal)
        && int.TryParse(preset["ooxml-".Length..], out var id) ? id : null;

    private Entry? Match(AnimationCategory category, Func<Entry, bool> predicate) =>
        _entries.FirstOrDefault(e => e.Category == category && predicate(e));

    private Entry DefaultFor(AnimationCategory category) =>
        Match(category, e => e.Canonical is "fade" or "disappear" or "line")
        ?? _entries.First(e => e.Category == category);
}

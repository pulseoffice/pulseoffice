using System.Text.Json;

namespace PulseOffice.Presentations;

public interface IAnimationBehaviourLibrary
{
    AnimationBehaviours? Find(AnimationCategory category, string odfPresetId, string? subtype);

    IReadOnlyList<AnimationBehaviourPreset> All { get; }
}

public sealed record AnimationBehaviourPreset
{
    public AnimationCategory Category { get; init; }

    public required string Preset { get; init; }

    public string? Subtype { get; init; }

    public IReadOnlyList<AnimationBehaviour> Nodes { get; init; } = [];

    public string? IterateType { get; init; }

    public double? IterateInterval { get; init; }

    public bool TextOnly { get; init; }

    public BehaviourTiming RootTiming { get; init; } = new();

    public AnimationBehaviours Behaviours => new()
    {
        Nodes = Nodes, IterateType = IterateType, IterateInterval = IterateInterval, TextOnly = TextOnly, RootTiming = RootTiming,
    };
}

public sealed class AnimationBehaviourLibrary : IAnimationBehaviourLibrary
{
    private const string ResourceName = "PulseOffice.Presentations.animation-behaviours.json";

    private readonly Lazy<IReadOnlyList<AnimationBehaviourPreset>> _presets = new(Load);

    public IReadOnlyList<AnimationBehaviourPreset> All => _presets.Value;

    public AnimationBehaviours? Find(AnimationCategory category, string odfPresetId, string? subtype)
    {
        var candidates = _presets.Value.Where(preset => preset.Category == category && preset.Preset == odfPresetId).ToList();
        return (candidates.FirstOrDefault(preset => preset.Subtype == subtype) ?? candidates.FirstOrDefault())?.Behaviours;
    }

    private static IReadOnlyList<AnimationBehaviourPreset> Load()
    {
        using var stream = typeof(AnimationBehaviourLibrary).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing resource {ResourceName}");
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            AllowOutOfOrderMetadataProperties = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };
        return JsonSerializer.Deserialize<List<AnimationBehaviourPreset>>(stream, options) ?? [];
    }
}

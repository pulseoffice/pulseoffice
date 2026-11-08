namespace PulseOffice.Presentations;

public interface IAnimationBehaviourResolver
{
    AnimationBehaviours Resolve(AnimationEffect effect);

    AnimationBehaviours Fallback(AnimationEffect effect);
}

public sealed class AnimationBehaviourResolver(IAnimationBehaviourLibrary library, IAnimationPresetRegistry presets) : IAnimationBehaviourResolver
{
    public AnimationBehaviourResolver() : this(new AnimationBehaviourLibrary(), new AnimationPresetRegistry())
    {
    }

    private readonly IReadOnlyDictionary<AnimationCategory, string> _fallbacks = new Dictionary<AnimationCategory, string>
    {
        [AnimationCategory.Entrance] = "ooo-entrance-fade-in",
        [AnimationCategory.Exit] = "ooo-exit-fade-out",
        [AnimationCategory.Emphasis] = "ooo-emphasis-transparency",
        [AnimationCategory.MotionPath] = "ooo-motionpath-down",
        [AnimationCategory.Media] = "play",
    };

    public AnimationBehaviours Resolve(AnimationEffect effect)
    {
        if (effect.Native is { Behaviours: { Nodes.Count: > 0 } native } source
            && source.Category == effect.Category && source.Preset == effect.Preset
            && source.Subtype == effect.Subtype && source.MotionPath == effect.MotionPath)
            return native.ScaledTo(effect.DurationMs);

        var found = library.Find(effect.Category, presets.OdfPresetId(effect.Category, effect.Preset), effect.Subtype);
        return Routed(effect, found is { Nodes.Count: > 0 } ? found : Default(effect));
    }

    public AnimationBehaviours Fallback(AnimationEffect effect) => Routed(effect, Default(effect));

    private AnimationBehaviours Default(AnimationEffect effect) =>
        library.Find(effect.Category, _fallbacks[effect.Category], null) ?? new AnimationBehaviours();

    private AnimationBehaviours Routed(AnimationEffect effect, AnimationBehaviours found)
    {
        var scaled = found.ScaledTo(effect.DurationMs);
        if (string.IsNullOrEmpty(effect.MotionPath)) return scaled;
        var routed = scaled with { Nodes = [.. scaled.Nodes.Select(node => WithPath(node, effect.MotionPath))] };
        return routed.Nodes.Any(HasMotion)
            ? routed
            : routed with
            {
                Nodes =
                [
                    .. routed.Nodes,
                    new MotionBehaviour
                    {
                        Path = effect.MotionPath,
                        Timing = new BehaviourTiming { Duration = Math.Max(1, effect.DurationMs) / 1000.0, Fill = "hold" },
                    },
                ],
            };
    }

    private bool HasMotion(AnimationBehaviour node) =>
        node is MotionBehaviour || node is GroupBehaviour group && group.Children.Any(HasMotion);

    private AnimationBehaviour WithPath(AnimationBehaviour node, string path) => node switch
    {
        MotionBehaviour motion => motion with { Path = path },
        GroupBehaviour group => group with { Children = [.. group.Children.Select(child => WithPath(child, path))] },
        _ => node,
    };
}

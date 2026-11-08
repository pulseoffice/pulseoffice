namespace PulseOffice.Presentations;

public readonly record struct PresetPreviewFrame(double Opacity, double OffsetX, double OffsetY, double Scale, double Rotation, double Reveal)
{
    public static readonly PresetPreviewFrame Rest = new(1, 0, 0, 1, 0, 1);
}

public interface IPresetPreviewMotion
{
    bool Matches(string preset);

    PresetPreviewFrame Entering(double eased, double progress);
}

public interface IAnimationPresetPreview
{
    PresetPreviewFrame At(AnimationCategory category, string preset, double progress);
}

public sealed class AnimationPresetPreview(IReadOnlyList<IPresetPreviewMotion> motions) : IAnimationPresetPreview
{
    public AnimationPresetPreview() : this(
    [
        new StepPreviewMotion("appear", "disappear", "flash-once"),
        new FlyPreviewMotion(),
        new ZoomPreviewMotion(),
        new RevealPreviewMotion("wipe", "blinds", "strips", "random-bars", "checkerboard", "split", "box", "circle", "diamond", "plus", "wedge", "wheel", "peek-in", "peek-out"),
        new SpinPreviewMotion(),
        new GrowPreviewMotion(),
        new TeeterPreviewMotion(),
        new BouncePreviewMotion(),
        new PulsePreviewMotion(),
        new PathPreviewMotion(),
        new FadePreviewMotion(),
    ])
    {
    }

    public PresetPreviewFrame At(AnimationCategory category, string preset, double progress)
    {
        var t = Math.Clamp(progress, 0, 1);
        var eased = t * t * (3 - 2 * t);
        var motion = motions.FirstOrDefault(candidate => candidate.Matches(preset ?? string.Empty)) ?? throw new InvalidOperationException("No preview motion matches the preset.");
        return category == AnimationCategory.Exit ? Reverse(motion.Entering(1 - eased, 1 - t)) : motion.Entering(eased, t);
    }

    private PresetPreviewFrame Reverse(PresetPreviewFrame frame) => frame with { OffsetX = -frame.OffsetX, OffsetY = -frame.OffsetY };
}

public sealed class FadePreviewMotion : IPresetPreviewMotion
{
    public bool Matches(string preset) => true;

    public PresetPreviewFrame Entering(double eased, double progress) => PresetPreviewFrame.Rest with { Opacity = eased };
}

public sealed class StepPreviewMotion(params string[] presets) : IPresetPreviewMotion
{
    public bool Matches(string preset) => presets.Contains(preset, StringComparer.Ordinal);

    public PresetPreviewFrame Entering(double eased, double progress) => PresetPreviewFrame.Rest with { Opacity = progress >= 0.5 ? 1 : 0 };
}

public sealed class FlyPreviewMotion : IPresetPreviewMotion
{
    public bool Matches(string preset) => preset.StartsWith("fly", StringComparison.Ordinal) || preset.StartsWith("crawl", StringComparison.Ordinal)
        || preset is "float";

    public PresetPreviewFrame Entering(double eased, double progress) =>
        PresetPreviewFrame.Rest with { Opacity = Math.Min(1, eased * 2), OffsetY = (1 - eased) * 0.6 };
}

public sealed class ZoomPreviewMotion : IPresetPreviewMotion
{
    public bool Matches(string preset) => preset is "zoom" or "stretchy" or "grow-and-turn" or "spiral-in" or "swivel";

    public PresetPreviewFrame Entering(double eased, double progress) =>
        PresetPreviewFrame.Rest with { Opacity = eased, Scale = 0.2 + 0.8 * eased, Rotation = 0 };
}

public sealed class RevealPreviewMotion(params string[] presets) : IPresetPreviewMotion
{
    public bool Matches(string preset) => presets.Contains(preset, StringComparer.Ordinal);

    public PresetPreviewFrame Entering(double eased, double progress) => PresetPreviewFrame.Rest with { Reveal = eased };
}

public sealed class SpinPreviewMotion : IPresetPreviewMotion
{
    public bool Matches(string preset) => preset is "spin";

    public PresetPreviewFrame Entering(double eased, double progress) => PresetPreviewFrame.Rest with { Rotation = 360 * eased };
}

public sealed class GrowPreviewMotion : IPresetPreviewMotion
{
    public bool Matches(string preset) => preset is "grow";

    public PresetPreviewFrame Entering(double eased, double progress) => PresetPreviewFrame.Rest with { Scale = 1 + 0.35 * Math.Sin(progress * Math.PI) };
}

public sealed class TeeterPreviewMotion : IPresetPreviewMotion
{
    public bool Matches(string preset) => preset is "teeter" or "wave";

    public PresetPreviewFrame Entering(double eased, double progress) =>
        PresetPreviewFrame.Rest with { Rotation = 12 * Math.Sin(progress * Math.PI * 4) * (1 - progress) };
}

public sealed class BouncePreviewMotion : IPresetPreviewMotion
{
    public bool Matches(string preset) => preset is "bounce";

    public PresetPreviewFrame Entering(double eased, double progress) =>
        PresetPreviewFrame.Rest with { Opacity = Math.Min(1, progress * 3), OffsetY = -0.6 * Math.Abs(Math.Cos(progress * Math.PI * 2.5)) * (1 - progress) };
}

public sealed class PulsePreviewMotion : IPresetPreviewMotion
{
    public bool Matches(string preset) => preset is "pulse" or "flash" or "blink" or "bold-flash" or "transparency" or "color";

    public PresetPreviewFrame Entering(double eased, double progress) =>
        PresetPreviewFrame.Rest with { Opacity = 1 - 0.7 * Math.Abs(Math.Sin(progress * Math.PI * 2)) };
}

public sealed class PathPreviewMotion : IPresetPreviewMotion
{
    public bool Matches(string preset) => preset is "line" or "curve";

    public PresetPreviewFrame Entering(double eased, double progress) =>
        PresetPreviewFrame.Rest with { OffsetX = -0.5 + eased, OffsetY = -0.35 * Math.Sin(eased * Math.PI) };
}

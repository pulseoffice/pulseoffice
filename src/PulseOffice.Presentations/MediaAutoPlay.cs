namespace PulseOffice.Presentations;

public sealed class MediaAutoPlay
{
    public const string PlayPreset = "play";

    public IReadOnlyList<AnimationEffect> Animations(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        var automatic = slide.Elements.OfType<MediaElement>().Where(media => media.AutoPlay).ToList();
        if (automatic.Count == 0) return slide.Animations;
        return
        [
            .. automatic.Select((media, index) => new AnimationEffect
            {
                Id = $"{media.Id}.autoplay",
                TargetId = media.Id,
                Category = AnimationCategory.Media,
                Preset = PlayPreset,
                Trigger = index == 0 ? AnimationTrigger.AfterPrevious : AnimationTrigger.WithPrevious,
                DurationMs = 1,
            }),
            .. slide.Animations,
        ];
    }

    public IReadOnlyList<MediaElement> Starting(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        return [.. slide.Elements.OfType<MediaElement>().Where(media => media.AutoPlay && !media.Hidden)];
    }

    public void Absorb(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        foreach (var effect in slide.Animations.Where(effect => !effect.IsTriggered).ToList())
        {
            if (!StartsWithTheSlide(effect) || slide.Find(effect.TargetId) is not MediaElement media) return;
            slide.Replace(media with { AutoPlay = true });
            slide.RemoveAnimation(effect.Id);
        }
    }

    private bool StartsWithTheSlide(AnimationEffect effect) =>
        effect.Category == AnimationCategory.Media
        && string.Equals(effect.Preset, PlayPreset, StringComparison.Ordinal)
        && effect.Trigger != AnimationTrigger.OnClick
        && effect.DelayMs == 0
        && effect.Paragraph is null
        && string.IsNullOrEmpty(effect.SoundPath);
}

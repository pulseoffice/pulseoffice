using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Motion;

public sealed record AnimationPresetChoice(AnimationCategory Category, string Preset);

public interface IAnimationLooks
{
    string ColorKey(AnimationCategory category);

    string IconKey(AnimationCategory category);

    string CategoryName(AnimationCategory category);

    string PresetName(string preset);

    string TriggerName(AnimationTrigger trigger);
}

public sealed class AnimationLooks(ITextCatalog texts) : IAnimationLooks
{
    public string ColorKey(AnimationCategory category) => category switch
    {
        AnimationCategory.Entrance => Palette.Keys.Success,
        AnimationCategory.Emphasis => Palette.Keys.Warning,
        AnimationCategory.Exit => Palette.Keys.Danger,
        AnimationCategory.MotionPath => Palette.Keys.Info,
        _ => Palette.Keys.TextSecondary,
    };

    public string IconKey(AnimationCategory category) => $"deck.motion.category.{category.ToString().ToLowerInvariant()}";

    public string CategoryName(AnimationCategory category) => texts[$"deck.anim.{category.ToString().ToLowerInvariant()}"];

    public string PresetName(string preset)
    {
        var key = $"deck.preset.{preset}";
        return texts.Contains(key) ? texts[key] : preset;
    }

    public string TriggerName(AnimationTrigger trigger) =>
        texts[$"deck.trigger.{char.ToLowerInvariant(trigger.ToString()[0])}{trigger.ToString()[1..]}"];
}

public sealed class AnimationPresetThumbnail : UserControl
{
    private const int PlayMs = 900;
    private const int HoldMs = 400;

    private readonly IAnimationPresetPreview _preview;
    private readonly AnimationPresetChoice _choice;
    private readonly Border _actor;
    private readonly Size _size;
    private DispatcherTimer? _timer;
    private DateTime _started;

    public AnimationPresetThumbnail(AnimationPresetChoice choice, IAnimationPresetPreview preview, IAnimationLooks looks, IIconCatalog icons, Size size)
    {
        _choice = choice ?? throw new ArgumentNullException(nameof(choice));
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        ArgumentNullException.ThrowIfNull(looks);
        ArgumentNullException.ThrowIfNull(icons);
        _size = size;

        var glyphSize = Math.Min(size.Width, size.Height) * 0.5;
        Control glyph = icons.Find(looks.IconKey(choice.Category)) is { } geometry
            ? new IconView(geometry, glyphSize).Themed(IconView.ForegroundProperty, looks.ColorKey(choice.Category))
            : new Border { Width = glyphSize, Height = glyphSize }.Themed(Border.BackgroundProperty, looks.ColorKey(choice.Category));
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        glyph.VerticalAlignment = VerticalAlignment.Center;

        _actor = new Border
        {
            Width = size.Width,
            Height = size.Height,
            Child = glyph,
            RenderTransformOrigin = RelativePoint.Center,
        };

        Content = new Border { Width = size.Width, Height = size.Height, ClipToBounds = true, Child = _actor }
            .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        ShowAt(1);
        PointerEntered += (_, _) => Play();
        PointerExited += (_, _) => Stop();
        DetachedFromVisualTree += (_, _) => Stop();
    }

    public AnimationPresetChoice Choice => _choice;

    public bool IsPlaying => _timer is not null;

    public PresetPreviewFrame Frame { get; private set; } = PresetPreviewFrame.Rest;

    public void ShowAt(double progress)
    {
        Frame = _preview.At(_choice.Category, _choice.Preset, progress);
        _actor.Opacity = Math.Clamp(Frame.Opacity, 0, 1);
        _actor.RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(Frame.Scale, Frame.Scale),
                new RotateTransform(Frame.Rotation),
                new TranslateTransform(Frame.OffsetX * _size.Width, Frame.OffsetY * _size.Height),
            },
        };
        _actor.Clip = Frame.Reveal >= 1 ? null : new RectangleGeometry(new Rect(0, 0, _size.Width * Math.Clamp(Frame.Reveal, 0, 1), _size.Height));
    }

    public void Play()
    {
        Stop();
        _started = DateTime.UtcNow;
        ShowAt(0);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) =>
        {
            var elapsed = (DateTime.UtcNow - _started).TotalMilliseconds;
            if (elapsed >= PlayMs + HoldMs) { _started = DateTime.UtcNow; ShowAt(0); return; }
            ShowAt(elapsed / PlayMs);
        };
        _timer.Start();
    }

    public void Stop()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer = null;
        ShowAt(1);
    }
}

public sealed class AnimationPresetPainter(IAnimationPresetPreview preview, IAnimationLooks looks, IIconCatalog icons) : IThumbnailPainter<AnimationPresetChoice>
{
    public Control Create(AnimationPresetChoice item, Size size) => new AnimationPresetThumbnail(item, preview, looks, icons, size);

    public void Repaint(Control thumbnail, AnimationPresetChoice item)
    {
    }
}

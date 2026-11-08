using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Settings;

public sealed class SwitchToggle : Control
{
    private const double TrackWidth = 34;
    private const double TrackHeight = 18;
    private const double KnobInset = 3;

    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<SwitchToggle, bool>(nameof(IsOn));

    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<SwitchToggle, IBrush?>(nameof(Accent));

    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<SwitchToggle, IBrush?>(nameof(Track));

    public static readonly StyledProperty<IBrush?> KnobProperty =
        AvaloniaProperty.Register<SwitchToggle, IBrush?>(nameof(Knob));

    static SwitchToggle()
    {
        AffectsRender<SwitchToggle>(IsOnProperty, AccentProperty, TrackProperty, KnobProperty, IsEnabledProperty);
        FocusableProperty.OverrideDefaultValue<SwitchToggle>(true);
    }

    public SwitchToggle()
    {
        Width = TrackWidth;
        Height = TrackHeight;
        Cursor = new Cursor(StandardCursorType.Hand);
        Bind(AccentProperty, this.GetResourceObservable("PulseAccent"));
        Bind(TrackProperty, this.GetResourceObservable(Palette.Keys.FieldBorder));
        Bind(KnobProperty, this.GetResourceObservable(Palette.Keys.Surface));
    }

    public event EventHandler? Toggled;

    public bool IsOn
    {
        get => GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public IBrush? Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public IBrush? Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public IBrush? Knob
    {
        get => GetValue(KnobProperty);
        set => SetValue(KnobProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var track = new Rect(0, 0, TrackWidth, TrackHeight);
        var radius = TrackHeight / 2;
        var knobSize = TrackHeight - KnobInset * 2;
        var knobX = IsOn ? TrackWidth - KnobInset - knobSize : KnobInset;

        using var _ = context.PushOpacity(IsEffectivelyEnabled ? 1 : 0.45);
        context.DrawRectangle(IsOn ? Accent : Track, null, track, radius, radius);
        context.DrawEllipse(Knob ?? Brushes.White, null, new Rect(knobX, KnobInset, knobSize, knobSize));

        if (IsFocused && IsKeyboardFocusWithin)
            context.DrawRectangle(null, new Pen(Accent, 1.5), track.Inflate(2), radius + 2, radius + 2);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (e.InitialPressMouseButton != MouseButton.Left) return;
        if (!new Rect(Bounds.Size).Contains(e.GetPosition(this))) return;

        Flip();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key is not (Key.Space or Key.Enter)) return;

        Flip();
        e.Handled = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SwitchToggleAutomationPeer(this);

    internal void Flip()
    {
        if (!IsEffectivelyEnabled) return;

        IsOn = !IsOn;
        Toggled?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class SwitchToggleAutomationPeer(SwitchToggle owner) : ControlAutomationPeer(owner), IToggleProvider
{
    public ToggleState ToggleState => owner.IsOn ? ToggleState.On : ToggleState.Off;

    public void Toggle() => owner.Flip();

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;
}

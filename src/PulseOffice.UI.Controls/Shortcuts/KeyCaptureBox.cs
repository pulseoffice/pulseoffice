using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands.Shortcuts;
using PulseOffice.UI.Controls.Text;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Shortcuts;

public sealed class KeyCaptureBox : Border, IKeyCaptureTarget
{
    private readonly IShortcutCapture _capture;
    private readonly IGestureNotation _notation;
    private readonly TextBlock _text;
    private readonly string _idlePrompt;
    private readonly string _activePrompt;
    private string? _shown;

    public KeyCaptureBox(IShortcutCapture capture, IGestureNotation notation, string idlePrompt, string activePrompt, DesignTokens.Metrics metrics)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _notation = notation ?? throw new ArgumentNullException(nameof(notation));
        _idlePrompt = idlePrompt;
        _activePrompt = activePrompt;

        Focusable = true;
        Avalonia.Automation.AutomationProperties.SetName(this, idlePrompt);
        Cursor = new Cursor(StandardCursorType.Hand);
        MinHeight = metrics.ControlHeight + 6;
        Padding = new Thickness(metrics.SpacingMedium, metrics.SpacingSmall);
        CornerRadius = new CornerRadius(metrics.RadiusTight);
        BorderThickness = new Thickness(1);

        _text = new TextBlock
        {
            FontSize = metrics.FontSize + 1,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        Child = _text;

        this.Themed(BackgroundProperty, Palette.Keys.SurfaceSunken);
        Paint();

        PointerPressed += (_, e) =>
        {
            Focus();
            e.Handled = true;
        };
        GotFocus += (_, _) => Paint();
        LostFocus += (_, _) => Paint();
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    public event EventHandler<GestureChord>? Captured;

    public void Show(string? gesture)
    {
        _shown = gesture;
        Paint();
    }

    public void Capture(ShortcutGesture gesture)
    {
        if (_capture.Chord(gesture) is { } chord) Captured?.Invoke(this, chord);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift) return;
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None) return;

        e.Handled = true;
        if (_capture.Chord(e.Key, e.KeyModifiers) is { } chord) Captured?.Invoke(this, chord);
    }

    private void Paint()
    {
        var hasText = _shown is { Length: > 0 };
        _text.Text = IsFocused
            ? hasText ? $"{_notation.Display(_shown)}" : _activePrompt
            : hasText ? _notation.Display(_shown) : _idlePrompt;
        _text.FontWeight = hasText ? FontWeight.SemiBold : FontWeight.Normal;
        _text.Themed(TextBlock.ForegroundProperty, hasText ? Palette.Keys.TextPrimary : Palette.Keys.TextSecondary);
        this.Themed(BorderBrushProperty, IsFocused ? "PulseAccent" : Palette.Keys.FieldBorder);
        BorderThickness = new Thickness(IsFocused ? 2 : 1);
    }
}

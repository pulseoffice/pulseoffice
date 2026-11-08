using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Notifications;

public sealed class NotificationToastView : Border
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan ShakeDuration = TimeSpan.FromMilliseconds(200);
    private const double EnterOffset = 14;

    private readonly TranslateTransform _offset = new();
    private readonly Animation _shake;

    public NotificationToastView(
        NotificationEntry entry, IIconCatalog icons, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(texts);

        var metrics = DesignTokens.For(density);
        var look = new NotificationLook();
        var tone = look.Tone(entry.Notification.Severity);

        Focusable = false;
        MinWidth = 280;
        MaxWidth = 520;
        HorizontalAlignment = HorizontalAlignment.Center;
        CornerRadius = new CornerRadius(metrics.RadiusSoft);
        BorderThickness = new Thickness(1);
        ClipToBounds = false;
        BoxShadow = BoxShadows.Parse("0 12 32 -8 #40000000");
        RenderTransform = _offset;
        Opacity = 0;
        this.Themed(BackgroundProperty, Palette.Keys.SurfaceOverlay);
        this.Themed(BorderBrushProperty, Palette.Keys.Border);
        AutomationProperties.SetName(this, texts[look.NameKey(entry.Notification.Severity)]);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);

        Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = FadeDuration, Easing = new Avalonia.Animation.Easings.CubicEaseOut() },
        ];
        _offset.Transitions =
        [
            new DoubleTransition { Property = TranslateTransform.YProperty, Duration = FadeDuration, Easing = new Avalonia.Animation.Easings.CubicEaseOut() },
        ];
        _offset.Y = EnterOffset;

        var stripe = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(metrics.RadiusSoft, 0, 0, metrics.RadiusSoft),
        }.Themed(BackgroundProperty, tone);

        var glyph = icons.Find(look.Icon(entry.Notification.Severity)) is { } geometry
            ? (Control)new IconView(geometry, 20).Themed(IconView.ForegroundProperty, tone)
            : new Border { Width = 20 };
        glyph.VerticalAlignment = VerticalAlignment.Top;
        glyph.Margin = new Thickness(metrics.SpacingMedium, metrics.SpacingMedium, 0, metrics.SpacingMedium);

        var message = new TextBlock
        {
            Text = entry.Notification.Message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = metrics.FontSize + 1,
            LineHeight = metrics.LineHeight + 2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(metrics.SpacingMedium, metrics.SpacingMedium, metrics.SpacingSmall, metrics.SpacingMedium),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        var trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = metrics.SpacingSmall,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, metrics.SpacingSmall, metrics.SpacingSmall, metrics.SpacingSmall),
        };

        if (entry.Notification.Action is { } action)
        {
            ActionButton = new Button
            {
                Classes = { "dialogQuiet" },
                Focusable = false,
                Content = action.LabelIn(texts),
                FontWeight = FontWeight.SemiBold,
                Padding = new Thickness(metrics.SpacingMedium, 4),
                VerticalAlignment = VerticalAlignment.Center,
            }.Themed(Button.ForegroundProperty, tone);
            ActionButton.Click += (_, _) => ActionRequested?.Invoke(this, action);
            trailing.Children.Add(ActionButton);
        }

        CloseButton = new Button
        {
            Classes = { "dialogClose" },
            Focusable = false,
            Width = metrics.ControlHeight,
            Height = metrics.ControlHeight,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = icons.Find("ui.close") is { } close ? new IconView(close, metrics.IconSize) : new TextBlock { Text = "✕" },
        };
        ToolTip.SetTip(CloseButton, texts["notification.close"]);
        AutomationProperties.SetName(CloseButton, texts["notification.close"]);
        CloseButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        trailing.Children.Add(CloseButton);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        Grid.SetColumn(stripe, 0);
        Grid.SetColumn(glyph, 1);
        Grid.SetColumn(message, 2);
        Grid.SetColumn(trailing, 3);
        row.Children.Add(stripe);
        row.Children.Add(glyph);
        row.Children.Add(message);
        row.Children.Add(trailing);

        Child = row;

        _shake = BuildShake();
    }

    public NotificationEntry Entry { get; }

    public Button? ActionButton { get; }

    public Button CloseButton { get; }

    public event EventHandler<NotificationAction>? ActionRequested;

    public event EventHandler? CloseRequested;

    public void Enter()
    {
        Opacity = 1;
        _offset.Y = 0;
    }

    public void Leave() => Opacity = 0;

    public Task ShakeAsync() => _shake.RunAsync(this);

    public static TimeSpan LeaveDuration => FadeDuration;

    private static Animation BuildShake()
    {
        var animation = new Animation { Duration = ShakeDuration };
        foreach (var (cue, x) in new[] { (0.0, 0.0), (0.2, -7.0), (0.4, 6.0), (0.6, -4.0), (0.8, 2.0), (1.0, 0.0) })
        {
            animation.Children.Add(new KeyFrame
            {
                Cue = new Cue(cue),
                Setters = { new Setter(TranslateTransform.XProperty, x) },
            });
        }

        return animation;
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Notifications;

public sealed class NotificationHistoryButton : Button
{
    private readonly INotificationHistory _history;
    private readonly ITextCatalog _texts;
    private readonly Border _badge;
    private readonly TextBlock _count;
    private bool _open;

    public NotificationHistoryButton(
        INotificationHistory history, IIconCatalog icons, ITextCatalog texts, INotificationActionRunner runner,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        ArgumentNullException.ThrowIfNull(icons);
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        ArgumentNullException.ThrowIfNull(runner);

        var metrics = DesignTokens.For(density);

        _count = new TextBlock
        {
            FontSize = 9,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _badge = new Border
        {
            MinWidth = 14,
            Height = 14,
            Padding = new Thickness(3, 0),
            CornerRadius = new CornerRadius(7),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -5, -8, 0),
            Child = _count,
            IsVisible = false,
        }.Themed(Border.BackgroundProperty, Palette.Keys.Danger);

        var glyph = icons.Find("ui.notice.history") is { } geometry
            ? (Control)new IconView(geometry, 14).Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary)
            : new TextBlock { Text = "≡" };

        Content = new Panel { Children = { glyph, _badge } };
        Padding = new Thickness(metrics.SpacingSmall, 2);
        Margin = new Thickness(metrics.SpacingMedium, 0, metrics.SpacingSmall + 4, 0);
        MinHeight = 0;
        VerticalAlignment = VerticalAlignment.Center;
        Classes.Add("dialogQuiet");
        Background = Brushes.Transparent;
        Translate();

        var flyout = new Flyout
        {
            Placement = PlacementMode.TopEdgeAlignedRight,
            FlyoutPresenterClasses = { "plain" },
        };
        flyout.Opened += (_, _) => _open = true;
        flyout.Closed += (_, _) => _open = false;
        Click += (_, _) =>
        {
            flyout.Content = new NotificationHistoryView(history, icons, texts, runner, density);
            flyout.ShowAt(this);
            _history.MarkRead();
        };

        Show();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _history.Changed += OnChanged;
        _texts.LanguageChanged += OnLanguageChanged;
        Translate();
        Show();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _history.Changed -= OnChanged;
        _texts.LanguageChanged -= OnLanguageChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Translate();

    private void Translate()
    {
        ToolTip.SetTip(this, _texts["notification.history.tip"]);
        Avalonia.Automation.AutomationProperties.SetName(this, _texts["notification.history.tip"]);
    }

    public bool IsHistoryOpen => _open;

    private void OnChanged(object? sender, EventArgs e) => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        if (_open && _history.Unread > 0) _history.MarkRead();
        Show();
    }

    private void Show()
    {
        var unread = _history.Unread;
        _badge.IsVisible = unread > 0;
        _count.Text = unread > 99 ? "99+" : unread.ToString(CultureInfo.CurrentCulture);
    }
}

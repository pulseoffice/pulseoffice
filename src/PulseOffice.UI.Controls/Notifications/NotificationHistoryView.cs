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

public sealed class NotificationHistoryView : Border
{
    private const double ShadowRoom = 10;

    private readonly INotificationHistory _history;
    private readonly IIconCatalog _icons;
    private readonly ITextCatalog _texts;
    private readonly INotificationActionRunner _runner;
    private readonly DesignTokens.Metrics _metrics;
    private readonly NotificationLook _look = new();
    private readonly StackPanel _rows;
    private readonly Button _clear;

    public NotificationHistoryView(
        INotificationHistory history, IIconCatalog icons, ITextCatalog texts, INotificationActionRunner runner,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _icons = icons ?? throw new ArgumentNullException(nameof(icons));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _metrics = DesignTokens.For(density);

        Width = 420;
        Margin = new Thickness(0, 0, ShadowRoom, ShadowRoom);
        CornerRadius = new CornerRadius(_metrics.RadiusSoft);
        BorderThickness = new Thickness(1);
        BoxShadow = BoxShadows.Parse("0 10 28 -6 #40101828");
        ClipToBounds = true;
        this.Themed(BackgroundProperty, Palette.Keys.SurfaceOverlay);
        this.Themed(BorderBrushProperty, Palette.Keys.Border);

        _clear = new Button { Content = texts["notification.history.clear"], FontSize = _metrics.FontSizeSmall };
        _clear.Classes.Add("dialogQuiet");
        _clear.Click += (_, _) => _history.Clear();

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall, _metrics.SpacingSmall, _metrics.SpacingSmall),
        };
        header.Children.Add(new TextBlock
        {
            Text = texts["notification.history.title"].ToUpper(CultureInfo.CurrentCulture),
            FontSize = _metrics.FontSizeSmall,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.6,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        Grid.SetColumn(_clear, 1);
        header.Children.Add(_clear);

        _rows = new StackPanel();

        var body = new DockPanel();
        var divider = new Border { Height = 1 }.Themed(BackgroundProperty, Palette.Keys.Border);
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(divider, Dock.Top);
        body.Children.Add(header);
        body.Children.Add(divider);
        body.Children.Add(new ScrollViewer
        {
            MaxHeight = 420,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _rows,
        });

        Child = body;
        Fill();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _history.Changed += OnChanged;
        Fill();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _history.Changed -= OnChanged;
        base.OnDetachedFromVisualTree(e);
    }

    public int RowCount => _rows.Children.Count(child => child is not TextBlock);

    private void OnChanged(object? sender, EventArgs e) => Avalonia.Threading.Dispatcher.UIThread.Post(Fill);

    private void Fill()
    {
        _rows.Children.Clear();
        var entries = _history.Entries;
        _clear.IsEnabled = entries.Count > 0;

        if (entries.Count == 0)
        {
            _rows.Children.Add(new TextBlock
            {
                Text = _texts["notification.history.empty"],
                FontSize = _metrics.FontSize,
                Margin = new Thickness(_metrics.SpacingMedium, _metrics.SpacingLarge),
                HorizontalAlignment = HorizontalAlignment.Center,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
            return;
        }

        foreach (var entry in entries) _rows.Children.Add(Row(entry));
    }

    private Control Row(NotificationRecord record)
    {
        var notification = record.Notification;
        var tone = _look.Tone(notification.Severity);

        var glyph = _icons.Find(_look.Icon(notification.Severity)) is { } geometry
            ? (Control)new IconView(geometry, 16).Themed(IconView.ForegroundProperty, tone)
            : new Border { Width = 16 };
        glyph.VerticalAlignment = VerticalAlignment.Top;
        glyph.Margin = new Thickness(0, 1, 0, 0);
        ToolTip.SetTip(glyph, _texts[_look.NameKey(notification.Severity)]);

        var message = new TextBlock
        {
            Text = notification.Message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = _metrics.FontSize,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall };
        meta.Children.Add(new TextBlock
        {
            Text = record.At.ToLocalTime().ToString("T", CultureInfo.CurrentCulture),
            FontSize = _metrics.FontSizeSmall,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        if (record.Repeats > 1)
            meta.Children.Add(new TextBlock
            {
                Text = _texts.Format("notification.history.repeats", record.Repeats),
                FontSize = _metrics.FontSizeSmall,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        var text = new StackPanel { Spacing = 2, Children = { message, meta } };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = _metrics.SpacingSmall + 2 };
        grid.Children.Add(glyph);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        if (notification.Action is { } action)
        {
            var run = new Button
            {
                Content = action.LabelIn(_texts),
                FontSize = _metrics.FontSizeSmall,
                VerticalAlignment = VerticalAlignment.Top,
            };
            run.Classes.Add("dialogQuiet");
            run.Click += (_, _) => _runner.Run(action);
            Grid.SetColumn(run, 2);
            grid.Children.Add(run);
        }

        return new Border
        {
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall + 2),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
    }
}

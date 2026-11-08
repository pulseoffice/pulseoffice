using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class TitleBar : ContentControl
{
    private readonly IWindowChrome _chrome;
    private readonly DesignTokens.Metrics _metrics;
    private readonly ITextCatalog _texts;

    public TitleBar(IWindowChrome chrome, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = DesignTokens.For(density);

        Height = _metrics.TitleBarHeight;
        Build();
    }

    public Panel MenuHost { get; } = new StackPanel { Orientation = Orientation.Horizontal };

    public Panel CenterHost { get; } = new StackPanel
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    public Panel TitleHost { get; } = new StackPanel
    {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public Button? CommandSearch { get; private set; }

    public void UseCommandSearch(IconGeometry? icon, string textKey, string gesture, Action open)
    {
        ArgumentNullException.ThrowIfNull(open);

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (icon is { } geometry)
            content.Children.Add(new IconView(geometry, _metrics.IconSize - 2)
            {
                VerticalAlignment = VerticalAlignment.Center,
            }.Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary));

        _searchLabel = new TextBlock
        {
            FontSize = _metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _searchKey = textKey;
        content.Children.Add(_searchLabel);

        content.Children.Add(new TextBlock
        {
            Text = gesture,
            FontSize = _metrics.FontSizeSmall,
            Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextDisabled));

        var button = new Button
        {
            Content = content,
            Padding = new Thickness(_metrics.SpacingMedium, 2),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            BorderThickness = new Thickness(1),
            MinWidth = 260,
            Focusable = false,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };

        button.Themed(BackgroundProperty, Palette.Keys.Surface);
        button.Themed(BorderBrushProperty, Palette.Keys.Border);

        button.Click += (_, _) => open();

        WindowDecorationProperties.SetElementRole(button, WindowDecorationsElementRole.User);
        CommandSearch = button;
        ShowSearchText();

        CenterHost.Children.Clear();
        CenterHost.Children.Add(TitleHost);
        CenterHost.Children.Add(button);
    }

    private void Build()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(_chrome.LeadingInset, 0, _chrome.TrailingInset, 0),
        };

        Grid.SetColumn(MenuHost, 0);
        grid.Children.Add(MenuHost);

        CenterHost.Children.Add(TitleHost);

        Grid.SetColumn(CenterHost, 1);
        grid.Children.Add(CenterHost);

        if (_chrome.DrawsCaptionButtons)
        {
            var captions = new CaptionButtons(_texts);
            Grid.SetColumn(captions, 2);
            grid.Children.Add(captions);
        }

        Content = grid;
        Background = Brushes.Transparent;

        WindowDecorationProperties.SetElementRole(this, WindowDecorationsElementRole.TitleBar);
        WindowDecorationProperties.SetElementRole(MenuHost, WindowDecorationsElementRole.User);

        PointerPressed += OnPointerPressed;
        AttachedToVisualTree += (_, _) => _texts.LanguageChanged += OnLanguageChanged;
        DetachedFromVisualTree += (_, _) => _texts.LanguageChanged -= OnLanguageChanged;
        DoubleTapped += OnDoubleTapped;
    }

    private TextBlock? _searchLabel;

    private string? _searchKey;

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ShowSearchText();
    }

    private void ShowSearchText()
    {
        if (_searchLabel is null || _searchKey is null || CommandSearch is null) return;
        _searchLabel.Text = _texts[_searchKey];
        Avalonia.Automation.AutomationProperties.SetName(CommandSearch, _texts[_searchKey]);
    }


    private Window? Window => TopLevel.GetTopLevel(this) as Window;


    private void ToggleMaximize()
    {
        if (Window is not { } window) return;
        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (Interactive(e.Source)) return;

        Window?.BeginMoveDrag(e);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Interactive(e.Source)) return;
        ToggleMaximize();
    }

    private bool Interactive(object? source)
    {
        for (var element = source as Visual; element is not null && !ReferenceEquals(element, this); element = element.GetVisualParent())
        {
            if (element is Button or MenuItem or Menu) return true;
            if (WindowDecorationProperties.GetElementRole(element) is not (WindowDecorationsElementRole.None or WindowDecorationsElementRole.TitleBar))
                return true;
        }
        return false;
    }
}

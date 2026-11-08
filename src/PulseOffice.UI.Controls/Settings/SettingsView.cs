using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Settings;
using PulseOffice.Settings.Navigation;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Settings;

public sealed record SettingsViewServices(
    ISettingsService Settings,
    ISettingsNavigator Navigator,
    ISettingStates States,
    ISettingEditorFactory Editors,
    IIconCatalog Icons,
    ITextCatalog Texts);

public sealed class SettingsView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const double NavigationWidth = 236;

    private readonly SettingsViewServices _services;
    private DialogChrome? _chrome;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly TextBox _search = new();
    private readonly StackPanel _navigation = new() { Spacing = 2 };
    private readonly StackPanel _content = new();
    private readonly ScrollViewer _scroll = new();
    private readonly SwitchToggle _advanced = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _advancedLabel = new();
    private readonly Control _advancedRow;
    private readonly Dictionary<string, SettingRow> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Control> _anchors = new(StringComparer.Ordinal);
    private readonly Button _resetAll;
    private readonly Button _done;

    private IReadOnlyList<SettingsPage> _pages = [];
    private string? _pageId;

    public SettingsView(SettingsViewServices services, string? initialPage = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _pageId = initialPage;

        _search.TextChanged += (_, _) => Rebuild();
        _advanced.Toggled += (_, _) => Rebuild();
        _advancedRow = AdvancedRow();

        Footer = new DialogFooter()
            .WithAction(Texts["settings.resetAll"], ResetAll, out _resetAll)
            .WithAction(Texts["settings.done"], () => Completed?.Invoke(this, null), out _done, primary: true);

        Content = Layout();
        Translate();
        Rebuild();

        AttachedToVisualTree += (_, _) =>
        {
            Texts.LanguageChanged += OnLanguageChanged;
            _services.Settings.Changed += OnSettingChanged;
            _search.Focus();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            Texts.LanguageChanged -= OnLanguageChanged;
            _services.Settings.Changed -= OnSettingChanged;
        };
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    private ITextCatalog Texts => _services.Texts;

    private bool Searching => !string.IsNullOrWhiteSpace(_search.Text);

    private Control Layout()
    {
        var sidebar = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Width = NavigationWidth };
        var searchBox = SearchBox();
        var navigation = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, _metrics.SpacingMedium, 0, 0),
            Content = _navigation,
        };

        Grid.SetRow(navigation, 1);
        Grid.SetRow(_advancedRow, 2);
        sidebar.Children.Add(searchBox);
        sidebar.Children.Add(navigation);
        sidebar.Children.Add(_advancedRow);

        var divider = new Border { Width = 1, Margin = new Thickness(_metrics.SpacingLarge, 0, 0, 0) }
            .Themed(Border.BackgroundProperty, Palette.Keys.Border);

        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scroll.Content = _content;
        _content.Margin = new Thickness(_metrics.SpacingLarge + 4, 0, _metrics.SpacingLarge + 6, _metrics.SpacingLarge);

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), MinHeight = 460 };
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(_scroll, 2);
        layout.Children.Add(sidebar);
        layout.Children.Add(divider);
        layout.Children.Add(_scroll);

        return layout;
    }

    private Control SearchBox()
    {
        _search.Padding = new Thickness(30, _metrics.SpacingSmall, _metrics.SpacingSmall, _metrics.SpacingSmall);
        _search.VerticalContentAlignment = VerticalAlignment.Center;

        var host = new Panel();
        host.Children.Add(_search);

        if (_services.Icons.Find("ui.search") is { } geometry)
            host.Children.Add(new IconView(geometry, 14)
            {
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
            }.Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary));

        return host;
    }

    private Control AdvancedRow()
    {
        _advancedLabel.FontSize = _metrics.FontSizeSmall + 0.5;
        _advancedLabel.VerticalAlignment = VerticalAlignment.Center;
        _advancedLabel.TextWrapping = TextWrapping.Wrap;
        _advancedLabel.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = _metrics.SpacingSmall };
        Grid.SetColumn(_advanced, 1);
        grid.Children.Add(_advancedLabel);
        grid.Children.Add(_advanced);

        return new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(_metrics.SpacingSmall, _metrics.SpacingMedium, _metrics.SpacingSmall, 0),
            Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0),
            Child = grid,
            IsVisible = _services.Navigator.HasAdvanced,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
    }

    private void Translate()
    {
        _search.PlaceholderText = Texts["settings.search"];
        Avalonia.Automation.AutomationProperties.SetName(_search, Texts["settings.search"]);
        Avalonia.Automation.AutomationProperties.SetName(_advanced, Texts["settings.advanced"]);
        _advancedLabel.Text = Texts["settings.advanced"];
        _resetAll.Content = Texts["settings.resetAll"];
        _done.Content = Texts["settings.done"];
        ToolTip.SetTip(_resetAll, Texts["settings.resetAll.description"]);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        Translate();
        Rebuild();
    });

    private void OnSettingChanged(object? sender, SettingChangedEventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (_rows.TryGetValue(e.Key, out var row)) row.Refresh();
        UpdateResetAll();
    });

    private void Rebuild()
    {
        _pages = _services.Navigator.Pages(new SettingsQuery(_search.Text ?? string.Empty, _advanced.IsOn));

        if (_pageId is null || _pages.All(page => page.Id != _pageId))
            _pageId = _pages.FirstOrDefault()?.Id;

        BuildNavigation();
        BuildContent();
        UpdateResetAll();
    }

    private void BuildNavigation()
    {
        _navigation.Children.Clear();

        var moduleCaption = false;
        foreach (var page in _pages)
        {
            if (page.IsModule && !moduleCaption)
            {
                _navigation.Children.Add(Caption(Texts["settings.modules"], new Thickness(_metrics.SpacingSmall, _metrics.SpacingMedium, 0, 2)));
                moduleCaption = true;
            }

            _navigation.Children.Add(NavigationItem(page));
        }
    }

    private Button NavigationItem(SettingsPage page)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = _metrics.SpacingSmall + 2 };
        var icon = Icon(page, 16);
        var title = new TextBlock
        {
            Text = Title(page),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = _metrics.FontSize + 0.5,
        };

        Grid.SetColumn(title, 1);
        grid.Children.Add(icon);
        grid.Children.Add(title);

        if (Searching)
        {
            var count = new TextBlock
            {
                Text = page.Count.ToString(System.Globalization.CultureInfo.CurrentCulture),
                FontSize = _metrics.FontSizeSmall,
                VerticalAlignment = VerticalAlignment.Center,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
            Grid.SetColumn(count, 2);
            grid.Children.Add(count);
        }

        var button = new Button
        {
            Content = grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(_metrics.SpacingSmall + 2, 7),
        };

        button.Classes.Add("settingsNav");
        if (page.Id == _pageId) button.Classes.Add("selected");
        button.Click += (_, _) => Select(page.Id);

        return button;
    }

    private void Select(string pageId)
    {
        _pageId = pageId;

        if (Searching && _anchors.TryGetValue(pageId, out var anchor))
        {
            BuildNavigation();
            anchor.BringIntoView();
            return;
        }

        BuildNavigation();
        BuildContent();
        _scroll.Offset = default;
    }

    private void BuildContent()
    {
        _content.Children.Clear();
        _rows.Clear();
        _anchors.Clear();

        if (_pages.Count == 0)
        {
            _content.Children.Add(Empty());
            return;
        }

        var shown = Searching ? _pages : _pages.Where(page => page.Id == _pageId);

        foreach (var page in shown)
        {
            var header = PageHeader(page);
            _anchors[page.Id] = header;
            _content.Children.Add(header);

            foreach (var section in page.Sections)
                _content.Children.Add(Section(section));
        }
    }

    private Control PageHeader(SettingsPage page)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall + 2,
            Margin = new Thickness(0, _content.Children.Count == 0 ? 2 : _metrics.SpacingLarge * 1.5, 0, _metrics.SpacingSmall),
        };

        stack.Children.Add(Icon(page, 20));
        stack.Children.Add(new TextBlock
        {
            Text = Title(page),
            FontSize = _metrics.FontSizeHeading + 2,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));

        return stack;
    }

    private Control Section(SettingsSection section)
    {
        var stack = new StackPanel { Margin = new Thickness(0, _metrics.SpacingMedium, 0, 0) };

        if (section.TitleKey is { } key)
            stack.Children.Add(Caption(Texts[key], new Thickness(2, _metrics.SpacingSmall, 0, _metrics.SpacingSmall)));

        var rows = new StackPanel();
        foreach (var definition in section.Settings)
        {
            if (rows.Children.Count > 0)
                rows.Children.Add(new Border { Height = 1, Margin = new Thickness(_metrics.SpacingLarge - 2, 0) }
                    .Themed(Border.BackgroundProperty, Palette.Keys.Border));

            var row = new SettingRow(definition, _services, _metrics);
            _rows[definition.Key] = row;
            rows.Children.Add(row.View);
        }

        stack.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft - 4),
            ClipToBounds = true,
            Child = rows,
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised)
         .Themed(Border.BorderBrushProperty, Palette.Keys.Border));

        return stack;
    }

    private Control Empty()
    {
        var stack = new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, _metrics.SpacingLarge * 5, 0, 0),
        };

        if (_services.Icons.Find("ui.search") is { } geometry)
            stack.Children.Add(new IconView(geometry, 32) { HorizontalAlignment = HorizontalAlignment.Center }
                .Themed(IconView.ForegroundProperty, Palette.Keys.TextDisabled));

        stack.Children.Add(new TextBlock
        {
            Text = Texts["settings.empty"],
            HorizontalAlignment = HorizontalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        return stack;
    }

    private DialogChrome Chrome => _chrome ??= new DialogChrome(_services.Icons, _metrics);

    private TextBlock Caption(string text, Thickness margin) =>
        Chrome.Caption(text, margin);

    private Control Icon(SettingsPage page, double size)
    {
        var geometry = _services.Icons.Find(page.IconKey) ?? _services.Icons.Find("settings.page");
        if (geometry is null) return new Border { Width = size, Height = size };

        var icon = new IconView(geometry, size) { VerticalAlignment = VerticalAlignment.Center };

        if (page.IsModule)
            icon.Foreground = new SolidColorBrush(Color.Parse(ModuleIdentity.For(page.Module).AccentHex));
        else
            icon.Bind(IconView.ForegroundProperty, icon.GetResourceObservable("PulseAccentText"));

        return icon;
    }

    private string Title(SettingsPage page) =>
        page.IsModule ? ModuleIdentity.For(page.Module).BrandName : Texts[page.TitleKey];

    private void UpdateResetAll() =>
        _resetAll.IsEnabled = _services.States.AnyResettable(_services.Settings.Definitions);

    private void ResetAll()
    {
        _services.States.ResetAll(_services.Settings.Definitions);
        Rebuild();
    }
}

public interface ISettingsDialog
{
    Task ShowAsync(CancellationToken ct = default);

    Task ShowAsync(string page, CancellationToken ct = default);
}

public sealed class SettingsDialog(IDialogService dialogs, SettingsViewServices services) : ISettingsDialog
{
    private readonly IDialogService _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    private readonly SettingsViewServices _services = services ?? throw new ArgumentNullException(nameof(services));

    public Task ShowAsync(CancellationToken ct = default) => Show(null, ct);

    public Task ShowAsync(string page, CancellationToken ct = default) => Show(page, ct);

    private Task Show(string? page, CancellationToken ct) => _dialogs.ShowAsync(
        new SettingsView(_services, page),
        new DialogOptions
        {
            TitleKey = "dialog.settings.title",
            PreferredWidth = 940,
            PreferredHeight = 660,
            AccessibleDescription = _services.Texts["dialog.settings.description"],
        },
        ct);
}

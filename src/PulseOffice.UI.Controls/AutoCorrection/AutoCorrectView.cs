using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.AutoCorrection;

public sealed record AutoCorrectViewServices(
    IAutoCorrectDraftFactory Drafts,
    IAutoCorrectDefaults Defaults,
    IReplacementQuery Query,
    IAutoCorrectListReader Lists,
    PulseOffice.Platform.IFileDialogService Files,
    IIconCatalog Icons,
    ITextCatalog Texts);

public sealed class AutoCorrectView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const double NavigationWidth = 210;

    private readonly AutoCorrectViewServices _services;
    private readonly AutoCorrectChrome _chrome;
    private readonly AutoCorrectDraft _draft;
    private readonly IReadOnlyList<AutoCorrectPage> _pages;
    private readonly List<Button> _navigation = [];
    private readonly TextBlock _heading;
    private readonly TextBlock _description;
    private readonly ComboBox _language = new() { MinWidth = 200 };
    private readonly StackPanel _languageRow;
    private readonly ContentControl _host = new();
    private IReadOnlyList<string> _languages = [];
    private int _page;

    public AutoCorrectView(AutoCorrectViewServices services, AutoCorrectProfile profile, string? language = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        ArgumentNullException.ThrowIfNull(profile);

        _chrome = new AutoCorrectChrome(services.Texts, services.Icons, DesignTokens.For(InterfaceDensity.Compact));
        _draft = services.Drafts.Create(profile);
        _pages =
        [
            new AutoCorrectOptionsPage(_chrome, _draft),
            new AutoCorrectReplacementsPage(_chrome, services.Query, new AutoCorrectImport(services.Files, services.Lists, services.Texts)),
            new AutoCorrectExceptionsPage(_chrome),
        ];

        _heading = _chrome.Heading(string.Empty);
        _description = _chrome.Description(string.Empty);
        _languageRow = LanguageRow(services.Defaults.Resolve(language));

        Content = Layout();

        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(Texts["autocorrect.apply"], () => Completed?.Invoke(this, _draft.Profile()), primary: true);

        Select(0);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    private ITextCatalog Texts => _services.Texts;

    private Control Layout()
    {
        var metrics = _chrome.Metrics;
        var navigation = new StackPanel { Spacing = 2, Width = NavigationWidth };

        for (var i = 0; i < _pages.Count; i++)
        {
            var index = i;
            var page = _pages[i];
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingMedium };
            row.Children.Add(_chrome.Icon(page.IconKey, 16));
            row.Children.Add(new TextBlock { Text = Texts[page.TitleKey], VerticalAlignment = VerticalAlignment.Center });

            var button = new Button
            {
                Content = row,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(metrics.SpacingSmall, 7),
                BorderThickness = default,
            };
            button.Classes.Add("ribbon");
            button.Click += (_, _) => Select(index);
            _navigation.Add(button);
            navigation.Children.Add(button);
        }

        var divider = new Border { Width = 1, Margin = new Thickness(metrics.SpacingLarge, 0) }
            .Themed(Border.BackgroundProperty, Palette.Keys.Border);

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var titles = new StackPanel { Spacing = 2, Children = { _heading, _description } };
        top.Children.Add(titles);
        Grid.SetColumn(_languageRow, 1);
        top.Children.Add(_languageRow);

        var main = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        _host.Margin = new Thickness(0, metrics.SpacingLarge, 0, 0);
        Grid.SetRow(_host, 1);
        main.Children.Add(top);
        main.Children.Add(_host);

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), MinHeight = 480 };
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(main, 2);
        layout.Children.Add(navigation);
        layout.Children.Add(divider);
        layout.Children.Add(main);
        return layout;
    }

    private StackPanel LanguageRow(string selected)
    {
        _languages = _services.Defaults.Languages;
        _language.ItemsSource = _languages.Select(_chrome.LanguageName).ToList();
        _language.SelectedIndex = Math.Max(0, _languages.ToList().FindIndex(tag => string.Equals(tag, selected, StringComparison.OrdinalIgnoreCase)));
        _language.SelectionChanged += (_, _) => ShowLanguage();
        Avalonia.Automation.AutomationProperties.SetName(_language, Texts["spelling.language"]);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _chrome.Metrics.SpacingSmall,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(_chrome.Metrics.SpacingLarge, 0, 0, 0),
            Children = { _chrome.Icon("ui.language", 16), _language },
        };
    }

    private void Select(int index)
    {
        _page = index;
        for (var i = 0; i < _navigation.Count; i++) _navigation[i].Classes.Set("checked", i == index);

        var page = _pages[index];
        _heading.Text = Texts[page.TitleKey];
        _description.Text = Texts[page.DescriptionKey];
        _languageRow.IsVisible = page.UsesLanguage;
        _host.Content = page.View;
        ShowLanguage();
    }

    private void ShowLanguage()
    {
        if (_languages.Count == 0 || _language.SelectedIndex < 0) return;

        var page = _pages[_page];
        if (page.UsesLanguage) page.ShowLanguage(_draft.For(_languages[_language.SelectedIndex]));
    }
}

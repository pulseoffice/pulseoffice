using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.AutoCorrection;

public sealed class AutoCorrectExceptionsPage(AutoCorrectChrome chrome) : AutoCorrectPage(chrome)
{
    private readonly WordListPanel _abbreviations = new(chrome, "autocorrect.exceptions.abbreviations", "autocorrect.problem.abbreviation");
    private readonly WordListPanel _twoCapitals = new(chrome, "autocorrect.exceptions.twoCapitals", "autocorrect.problem.twoCapitals");

    public override string TitleKey => "autocorrect.section.exceptions";

    public override string IconKey => "ui.list";

    public override string DescriptionKey => "autocorrect.section.exceptions.description";

    public override bool UsesLanguage => true;

    public override void ShowLanguage(LanguageDraft language)
    {
        _abbreviations.Show(language.Abbreviations);
        _twoCapitals.Show(language.TwoCapitalWords);
    }

    protected override Control Build()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        var left = _abbreviations.View;
        var right = _twoCapitals.View;
        left.Margin = new Thickness(0, 0, Chrome.Metrics.SpacingMedium, 0);
        right.Margin = new Thickness(Chrome.Metrics.SpacingMedium, 0, 0, 0);
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }
}

public sealed class WordListPanel
{
    private readonly AutoCorrectChrome _chrome;
    private readonly string _problemKey;
    private readonly TextBox _word;
    private readonly TextBlock _message;
    private readonly TextBlock _count = new();
    private readonly ListBox _list = new();
    private readonly Button _restore;
    private WordListDraft? _draft;

    public WordListPanel(AutoCorrectChrome chrome, string key, string problemKey)
    {
        _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
        _problemKey = problemKey;
        _word = chrome.Field(chrome.Texts[key + ".placeholder"]);
        Avalonia.Automation.AutomationProperties.SetName(_word, chrome.Texts[key]);
        _message = chrome.Message();
        _restore = chrome.Quiet(chrome.Texts["autocorrect.restoreDefaults"], () => _draft?.RestoreDefaults());
        View = Build(key);
    }

    public Control View { get; }

    public void Show(WordListDraft draft)
    {
        if (_draft is not null) _draft.Changed -= OnChanged;
        _draft = draft;
        _draft.Changed += OnChanged;
        _chrome.Show(_message, string.Empty);
        Refill();
    }

    private Control Build(string key)
    {
        var metrics = _chrome.Metrics;

        var title = new TextBlock { Text = _chrome.Texts[key], FontWeight = FontWeight.SemiBold };
        _count.FontSize = metrics.FontSizeSmall;
        _count.VerticalAlignment = VerticalAlignment.Center;
        _count.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        var titleRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        titleRow.Children.Add(title);
        Grid.SetColumn(_count, 1);
        titleRow.Children.Add(_count);

        _word.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            Add();
        };
        _word.TextChanged += (_, _) => _chrome.Show(_message, string.Empty);

        var add = _chrome.Primary(_chrome.Texts["autocorrect.add"], Add);
        add.Margin = new Thickness(metrics.SpacingSmall, 0, 0, 0);
        var addRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        addRow.Children.Add(_word);
        Grid.SetColumn(add, 1);
        addRow.Children.Add(add);

        _list.Classes.Add("compact");
        _list.ItemTemplate = new FuncDataTemplate<WordEntry>((entry, _) => entry is null ? new Border() : Row(entry), supportsRecycling: false);
        var frame = _chrome.Frame(_list);
        frame.Margin = new Thickness(0, metrics.SpacingSmall);

        var top = new StackPanel
        {
            Spacing = 4,
            Children = { titleRow, _chrome.Description(_chrome.Texts[key + ".description"]), addRow, _message },
        };

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetRow(frame, 1);
        Grid.SetRow(_restore, 2);
        _restore.HorizontalAlignment = HorizontalAlignment.Right;
        grid.Children.Add(top);
        grid.Children.Add(frame);
        grid.Children.Add(_restore);
        return grid;
    }

    private Control Row(WordEntry entry)
    {
        var delete = _chrome.IconButton("ui.delete", _chrome.Texts["autocorrect.delete"], () => _draft?.Remove(entry.Word));
        delete.Opacity = 0;

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), MinHeight = 26 };
        grid.Children.Add(new TextBlock
        {
            Text = entry.Word,
            FontFamily = new FontFamily("Menlo, Consolas, monospace"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(_chrome.Metrics.SpacingSmall, 0),
        });
        if (entry.Origin is EntryOrigin.User or EntryOrigin.Modified)
        {
            var badge = _chrome.OriginBadge(entry.Origin);
            Grid.SetColumn(badge, 1);
            grid.Children.Add(badge);
        }
        Grid.SetColumn(delete, 2);
        grid.Children.Add(delete);

        var row = new Border { Background = Brushes.Transparent, Child = grid };
        row.PointerEntered += (_, _) => delete.Opacity = 1;
        row.PointerExited += (_, _) => delete.Opacity = 0;
        return row;
    }

    private void Add()
    {
        if (_draft is null) return;

        var problem = _draft.Add(_word.Text ?? string.Empty);
        if (problem != EntryProblem.None)
        {
            _chrome.Show(_message, _chrome.Problem(problem, _problemKey));
            return;
        }

        _word.Text = string.Empty;
        _word.Focus();
    }

    private void OnChanged(object? sender, EventArgs e) => Refill();

    private void Refill()
    {
        if (_draft is null) return;

        var entries = _draft.Entries;
        _list.ItemsSource = entries;
        _count.Text = _chrome.Count(entries.Count);
        _restore.IsEnabled = !_draft.IsAtDefaults;
    }
}

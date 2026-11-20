using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Search;

public sealed record FindOption(string Key, string LabelKey, bool Initial = false);

public sealed record FindChoiceValue(string Value, string Label);

public sealed record FindChoice(string Key, string LabelKey, IReadOnlyList<FindChoiceValue> Values, bool ForReplacement = false);

public sealed record FindRequest(string Query, string Replacement, IReadOnlySet<string> Options)
{
    public IReadOnlyDictionary<string, string> Choices { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public bool Has(string option) => Options.Contains(option);

    public string? Choice(string key) => Choices.TryGetValue(key, out var value) && value.Length > 0 ? value : null;
}

public interface IFindReplaceTarget
{
    IReadOnlyList<FindOption> Options { get; }

    string InitialQuery { get; }

    bool IsReadOnly { get; }

    bool SupportsFindNext { get; }

    string? Summarize(FindRequest request);

    string FindNext(FindRequest request);

    string ReplaceAll(FindRequest request);

    bool SupportsReplace => false;

    string Replace(FindRequest request) => FindNext(request);

    IReadOnlyList<FindChoice> Choices => [];

    bool SupportsFindPrevious => false;

    string FindPrevious(FindRequest request) => FindNext(request);
}

public sealed class FindReplaceView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly IFindReplaceTarget _target;
    private readonly TextBox _query;
    private readonly TextBox _replacement;
    private readonly IReadOnlyList<(FindOption Option, CheckBox Box)> _options;
    private readonly TextBlock _summary;
    private readonly IReadOnlyList<(FindChoice Choice, ComboBox Box)> _choices;

    public FindReplaceView(IFindReplaceTarget target, ITextCatalog texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        _target = target ?? throw new ArgumentNullException(nameof(target));
        var metrics = DesignTokens.For(InterfaceDensity.Compact);

        _query = new TextBox { PlaceholderText = texts["find.query"], Text = target.InitialQuery };
        _replacement = new TextBox { PlaceholderText = texts["find.replacement"], IsEnabled = !target.IsReadOnly };
        Avalonia.Automation.AutomationProperties.SetName(_query, texts["find.query"]);
        Avalonia.Automation.AutomationProperties.SetName(_replacement, texts["find.replacement"]);
        _options = target.Options
            .Select(option => (option, new CheckBox { Content = texts[option.LabelKey], IsChecked = option.Initial, FontSize = metrics.FontSize }))
            .ToList();
        _summary = new TextBlock { FontSize = metrics.FontSize, TextWrapping = Avalonia.Media.TextWrapping.Wrap }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Avalonia.Automation.AutomationProperties.SetLiveSetting(_summary, Avalonia.Automation.AutomationLiveSetting.Polite);
        _summary.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBlock.TextProperty) Avalonia.Automation.AutomationProperties.SetName(_summary, _summary.Text ?? string.Empty);
        };

        var options = new StackPanel { Spacing = metrics.SpacingSmall / 2 };
        foreach (var (_, box) in _options) options.Children.Add(box);
        _choices = [.. target.Choices.Select(choice => (choice, Combo(choice, texts)))];

        var body = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            MinWidth = 420,
            Children = { _query, _replacement, options },
        };
        if (_choices.Count > 0) body.Children.Add(Choices(texts, metrics));
        body.Children.Add(_summary);
        Content = body;

        var footer = new DialogFooter(InterfaceDensity.Compact)
            .WithCancel(texts["find.close"], () => Completed?.Invoke(this, null));
        if (target.SupportsFindNext) footer.WithAction(texts["find.next"], FindNext, primary: true);
        if (target.SupportsReplace)
        {
            footer.WithAction(texts["find.replace"], Replace, out var replace);
            replace.IsEnabled = !target.IsReadOnly;
        }
        footer.WithAction(texts["find.replaceAll"], ReplaceAll, out var replaceAll, primary: !target.SupportsFindNext);
        replaceAll.IsEnabled = !target.IsReadOnly;
        Footer = footer;

        _query.TextChanged += (_, _) => Summarize();
        foreach (var (_, box) in _options) box.IsCheckedChanged += (_, _) => Summarize();
        foreach (var (_, box) in _choices) box.SelectionChanged += (_, _) => Summarize();
        Summarize();
    }

    private ComboBox Combo(FindChoice choice, ITextCatalog texts)
    {
        var box = new ComboBox
        {
            ItemsSource = choice.Values.Select(value => value.Label).ToList(),
            SelectedIndex = 0,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            IsEnabled = !choice.ForReplacement || !_target.IsReadOnly,
        };
        Avalonia.Automation.AutomationProperties.SetName(box, texts[choice.LabelKey]);
        return box;
    }

    private Control Choices(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,*"), ColumnSpacing = metrics.SpacingSmall, RowSpacing = metrics.SpacingSmall / 2 };
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.Children.Add(Header(texts["find.format.find"], 1, metrics));
        grid.Children.Add(Header(texts["find.format.replace"], 2, metrics));
        var rows = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (choice, box) in _choices)
        {
            if (!rows.TryGetValue(choice.LabelKey, out var row))
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                row = rows[choice.LabelKey] = grid.RowDefinitions.Count - 1;
                var label = new TextBlock { Text = texts[choice.LabelKey], FontSize = metrics.FontSize, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                Grid.SetRow(label, row);
                grid.Children.Add(label);
            }
            Grid.SetRow(box, row);
            Grid.SetColumn(box, choice.ForReplacement ? 2 : 1);
            grid.Children.Add(box);
        }
        return new Expander { Header = texts["find.format.title"], Content = grid, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
    }

    private TextBlock Header(string text, int column, DesignTokens.Metrics metrics)
    {
        var header = new TextBlock { Text = text, FontSize = metrics.FontSizeSmall }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Grid.SetColumn(header, column);
        return header;
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public string Summary => _summary.Text ?? string.Empty;

    private FindRequest Request() => new(
        _query.Text ?? string.Empty,
        _replacement.Text ?? string.Empty,
        _options.Where(pair => pair.Box.IsChecked == true).Select(pair => pair.Option.Key).ToHashSet())
    {
        Choices = _choices.ToDictionary(
            pair => pair.Choice.Key,
            pair => pair.Box.SelectedIndex is var index and >= 0 && index < pair.Choice.Values.Count ? pair.Choice.Values[index].Value : string.Empty,
            StringComparer.Ordinal),
    };

    private void Summarize()
    {
        if (_target.Summarize(Request()) is { } summary) _summary.Text = summary;
    }

    private void FindNext()
    {
        var request = Request();
        if (!Searchable(request)) return;
        _summary.Text = _target.FindNext(request);
    }

    private void Replace()
    {
        var request = Request();
        if (!Searchable(request) || _target.IsReadOnly) return;
        _summary.Text = _target.Replace(request);
    }

    private void ReplaceAll()
    {
        var request = Request();
        if (!Searchable(request) || _target.IsReadOnly) return;
        _summary.Text = _target.ReplaceAll(request);
    }

    private bool Searchable(FindRequest request) =>
        request.Query.Length > 0 || _choices.Any(pair => !pair.Choice.ForReplacement && request.Choice(pair.Choice.Key) is not null);
}

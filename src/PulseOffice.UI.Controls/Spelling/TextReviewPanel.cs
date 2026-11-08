using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Spelling;

public sealed class TextReviewPanel : UserControl
{
    private readonly ITextReviewSession _session;
    private readonly ITextCatalog _texts;
    private readonly IIconCatalog _icons;
    private readonly DesignTokens.Metrics _metrics;
    private readonly ContentControl _body = new();

    private readonly Action? _openDictionaries;

    public TextReviewPanel(ITextReviewSession session, ITextCatalog texts, IIconCatalog icons, InterfaceDensity density = InterfaceDensity.Compact, Action? openDictionaries = null)
    {
        _openDictionaries = openDictionaries;
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _icons = icons ?? throw new ArgumentNullException(nameof(icons));
        _metrics = DesignTokens.For(density);
        Content = new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Rebuild();
    }

    public ListBox? Suggestions { get; private set; }

    public TextBox? Custom { get; private set; }

    public ListBox? Languages { get; private set; }

    public PanelSearchBox? LanguageSearch { get; private set; }

    public IReadOnlyDictionary<string, Button> Actions => _actions;

    private readonly Dictionary<string, Button> _actions = [];

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session.IsDisplayed = true;
        _session.Changed -= OnChanged;
        _session.Changed += OnChanged;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _session.Changed -= OnChanged;
        _session.IsDisplayed = false;
        _session.Dismiss();
    }

    private void OnChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        _actions.Clear();
        Suggestions = null;
        Notice = null;
        Custom = null;
        Languages = null;
        LanguageSearch = null;

        Control content = _session.Pending switch
        {
            SpellingReviewRequest spelling => Spelling(spelling),
            ThesaurusReviewRequest thesaurus => Thesaurus(thesaurus),
            LanguageReviewRequest language => Language(language),
            MissingDictionariesRequest => MissingDictionaries(),
            _ => Idle(),
        };
        if (!_session.Invalidated)
        {
            _body.Content = content;
            return;
        }

        Notice = new TextBlock
        {
            Text = _texts[_session.Pending is null ? "textReview.changed.cancelled" : "textReview.changed.recheck"],
            TextWrapping = TextWrapping.Wrap,
            FontSize = _metrics.FontSizeSmall,
            Margin = new Thickness(_metrics.SpacingMedium, _metrics.SpacingMedium, _metrics.SpacingMedium, 0),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.Warning);
        var stack = new StackPanel();
        stack.Children.Add(Notice);
        stack.Children.Add(content);
        _body.Content = stack;
    }

    public TextBlock? Notice { get; private set; }

    private Control MissingDictionaries()
    {
        var parts = new List<Control> { Heading(_texts["textReview.noDictionaries.title"]), Secondary(_texts["textReview.noDictionaries"]) };
        if (_openDictionaries is { } open)
        {
            var button = new Button { Content = _texts["command.shell.tools.dictionaries.title"], HorizontalAlignment = HorizontalAlignment.Left };
            button.Classes.Add("dialogPrimary");
            button.Click += (_, _) =>
            {
                _session.Dismiss();
                open();
            };
            _actions["dictionaries"] = button;
            parts.Add(button);
        }

        return Stack([.. parts]);
    }

    private Control Idle() => Stack(
        Heading(_texts["textReview.idle.title"]),
        Secondary(_texts["textReview.idle"]));

    private Control Spelling(SpellingReviewRequest request)
    {
        var finding = request.Finding;
        var grammar = finding.Rule is not null;

        var word = new TextBlock
        {
            Text = finding.Word.Trim(),
            FontSize = _metrics.FontSize + 4,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextDecorations = TextDecorations.Underline,
        };
        word.Themed(TextBlock.ForegroundProperty, grammar ? Palette.Keys.Info : Palette.Keys.Danger);
        AutomationProperties.SetName(word, finding.Word);

        var card = new Border
        {
            Padding = new Thickness(_metrics.SpacingMedium),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            BorderThickness = new Thickness(1),
            Child = Stack(
                Secondary(grammar ? _texts[$"grammar.rule.{finding.Rule}"] : _texts["textReview.notInDictionary"]),
                word,
                Secondary(finding.Where)),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken).Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        var suggestions = new ListBox
        {
            ItemsSource = finding.Suggestions,
            MaxHeight = 220,
            SelectedIndex = finding.Suggestions.Count > 0 ? 0 : -1,
        };
        AutomationProperties.SetName(suggestions, _texts["spelling.prompt.suggestions"]);
        suggestions.DoubleTapped += (_, _) => Spell(SpellingDecision.Replace, grammar);
        suggestions.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Spell(SpellingDecision.Replace, grammar);
            e.Handled = true;
        };
        Suggestions = suggestions;

        Custom = new TextBox { PlaceholderText = _texts["spelling.prompt.custom.placeholder"] };
        AutomationProperties.SetName(Custom, _texts["spelling.prompt.custom"]);
        Custom.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Spell(SpellingDecision.Replace, grammar);
            e.Handled = true;
        };

        var replace = Stack(
            Action("replace", "spelling.prompt.replace", () => Spell(SpellingDecision.Replace, grammar), primary: true),
            Action("replaceAll", "spelling.prompt.replaceall", () => Spell(SpellingDecision.ReplaceAll, grammar)));
        var skip = Stack(
            Action("ignore", "spelling.prompt.ignore", () => Spell(SpellingDecision.Ignore, grammar)),
            Action("ignoreAll", "spelling.prompt.ignoreall", () => Spell(SpellingDecision.IgnoreAll, grammar)));
        if (!grammar) ((StackPanel)skip).Children.Add(Action("learn", "spelling.prompt.learn", () => Spell(SpellingDecision.Learn, grammar)));

        var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*") };
        Grid.SetColumn(skip, 2);
        buttons.Children.Add(replace);
        buttons.Children.Add(skip);

        var list = finding.Suggestions.Count > 0
            ? (Control)new PanelListFrame(_metrics, suggestions)
            : Secondary(_texts["spelling.prompt.noSuggestions"]);

        var focus = finding.Suggestions.Count > 0 ? (Control)suggestions : Custom;
        focus.AttachedToVisualTree += (_, _) => focus.Focus();

        return Stack(
            Heading(_texts[request.TitleKey]),
            card,
            Caption(_texts["spelling.prompt.suggestions"]),
            list,
            Custom,
            buttons,
            Action("stop", "textReview.stop", () => _session.Answer(new SpellingAnswer(SpellingDecision.Stop))));
    }

    private void Spell(SpellingDecision decision, bool grammar)
    {
        if (_session.Pending is not SpellingReviewRequest request) return;

        var typed = Custom?.Text?.Trim() ?? string.Empty;
        var suggestion = Suggestions?.SelectedItem as string ?? string.Empty;
        var replacement = typed.Length > 0 ? typed : suggestion;

        if (decision is SpellingDecision.Replace or SpellingDecision.ReplaceAll && replacement.Length == 0 && !grammar)
            decision = SpellingDecision.Ignore;

        _ = request;
        _session.Answer(new SpellingAnswer(decision, replacement));
    }

    private Control Thesaurus(ThesaurusReviewRequest request)
    {
        var list = new ListBox { MaxHeight = 360 };
        AutomationProperties.SetName(list, _texts["thesaurus.synonyms"]);
        var entries = new List<ThesaurusChoice?>();

        foreach (var group in request.Choices.GroupBy(choice => choice.PartOfSpeech))
        {
            if (group.Key.Length > 0)
            {
                list.Items.Add(new ListBoxItem
                {
                    Content = Caption(group.Key),
                    IsEnabled = false,
                    Focusable = false,
                });
                entries.Add(null);
            }

            foreach (var choice in group)
            {
                list.Items.Add(new ListBoxItem { Content = choice.Synonym });
                entries.Add(choice);
            }
        }

        list.SelectedIndex = entries.FindIndex(entry => entry is not null);
        Suggestions = list;

        void Choose()
        {
            if (list.SelectedIndex >= 0 && entries[list.SelectedIndex] is { } chosen) _session.Answer(chosen.Synonym);
        }

        list.DoubleTapped += (_, _) => Choose();
        list.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Choose();
            e.Handled = true;
        };
        list.AttachedToVisualTree += (_, _) => list.Focus();

        var word = new TextBlock { Text = request.Word, FontSize = _metrics.FontSize + 4, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };

        return Stack(
            Heading(_texts[request.TitleKey]),
            word,
            Caption(_texts["thesaurus.synonyms"]),
            new PanelListFrame(_metrics, list),
            Row(Action("apply", "textReview.replace", Choose, primary: true), Action("cancel", "prompt.cancel", _session.Dismiss)));
    }

    private Control Language(LanguageReviewRequest request)
    {
        var list = new ListBox { MaxHeight = 360 };
        AutomationProperties.SetName(list, _texts["spelling.language"]);
        var search = new PanelSearchBox(_icons, _metrics, _texts["textReview.language.search"]);
        var shown = new List<int>();

        void Fill()
        {
            var query = search.Text.Trim();
            list.Items.Clear();
            shown.Clear();
            for (var index = 0; index < request.Choices.Count; index++)
            {
                var choice = request.Choices[index];
                if (query.Length > 0
                    && !choice.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    && !choice.Code.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

                shown.Add(index);
                list.Items.Add(new ListBoxItem { Content = LanguageRow(choice) });
            }

            list.SelectedIndex = Math.Max(shown.IndexOf(request.Current), shown.Count > 0 ? 0 : -1);
        }

        void Choose()
        {
            if (list.SelectedIndex >= 0 && list.SelectedIndex < shown.Count) _session.Answer(shown[list.SelectedIndex]);
        }

        search.TextChanged += (_, _) => Fill();
        search.Descend += (_, _) => list.Focus();
        search.Submitted += (_, _) => Choose();
        list.DoubleTapped += (_, _) => Choose();
        list.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Choose();
            e.Handled = true;
        };
        Fill();
        Languages = list;
        LanguageSearch = search;

        return Stack(
            Heading(_texts[request.TitleKey]),
            search,
            new PanelListFrame(_metrics, list),
            Row(Action("apply", "textReview.apply", Choose, primary: true), Action("cancel", "prompt.cancel", _session.Dismiss)));
    }

    private Control LanguageRow(DictionaryLanguage choice)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(new TextBlock { Text = choice.Name, TextTrimming = TextTrimming.CharacterEllipsis });
        var code = new Border
        {
            Padding = new Thickness(5, 0),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock { Text = choice.Code, FontSize = _metrics.FontSizeSmall }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
        Grid.SetColumn(code, 1);
        grid.Children.Add(code);
        AutomationProperties.SetName(grid, $"{choice.Name} ({choice.Code})");
        return grid;
    }

    private Button Action(string key, string textKey, Action run, bool primary = false)
    {
        var button = new Button { Content = _texts[textKey], HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        button.Classes.Add(primary ? "dialogPrimary" : "dialogQuiet");
        button.Click += (_, _) => run();
        _actions[key] = button;
        return button;
    }

    private Control Row(Control left, Control right)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*") };
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private StackPanel Stack(params Control[] children)
    {
        var stack = new StackPanel { Spacing = _metrics.SpacingMedium };
        foreach (var child in children) stack.Children.Add(child);
        return stack;
    }

    private TextBlock Heading(string text) =>
        new() { Text = text.TrimEnd('…'), FontSize = _metrics.FontSize + 1, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };

    private TextBlock Caption(string text) =>
        new TextBlock { Text = text.ToUpper(System.Globalization.CultureInfo.CurrentCulture), FontSize = _metrics.FontSizeSmall, FontWeight = FontWeight.SemiBold, LetterSpacing = 0.5 }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private TextBlock Secondary(string text) =>
        new TextBlock { Text = text, FontSize = _metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
}

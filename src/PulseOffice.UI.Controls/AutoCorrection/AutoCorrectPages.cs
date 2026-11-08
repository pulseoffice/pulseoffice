using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Controls.Settings;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.AutoCorrection;

public abstract class AutoCorrectPage(AutoCorrectChrome chrome)
{
    private Control? _view;

    public abstract string TitleKey { get; }

    public abstract string IconKey { get; }

    public abstract string DescriptionKey { get; }

    public virtual bool UsesLanguage => false;

    public Control View => _view ??= Build();

    protected AutoCorrectChrome Chrome { get; } = chrome ?? throw new ArgumentNullException(nameof(chrome));

    public virtual void ShowLanguage(LanguageDraft language)
    {
    }

    protected abstract Control Build();
}

public sealed class AutoCorrectOptionsPage(AutoCorrectChrome chrome, AutoCorrectDraft draft) : AutoCorrectPage(chrome)
{
    private readonly List<Control> _dependent = [];

    public override string TitleKey => "autocorrect.section.options";

    public override string IconKey => "ui.check";

    public override string DescriptionKey => "autocorrect.section.options.description";

    protected override Control Build()
    {
        var rows = new StackPanel();

        rows.Children.Add(Row("autocorrect.enabled", draft.Enabled, value =>
        {
            draft.Enabled = value;
            foreach (var row in _dependent) row.IsEnabled = value;
        }, master: true));

        Option(rows, "autocorrect.replacements", options => options.Replacements, (options, value) => options with { Replacements = value });
        Option(rows, "autocorrect.capitalise", options => options.CapitaliseFirstLetter, (options, value) => options with { CapitaliseFirstLetter = value });
        Option(rows, "autocorrect.twoInitials", options => options.FixTwoInitialCapitals, (options, value) => options with { FixTwoInitialCapitals = value });
        Option(rows, "autocorrect.smartQuotes", options => options.SmartQuotes, (options, value) => options with { SmartQuotes = value });
        Option(rows, "autocorrect.dashes", options => options.Dashes, (options, value) => options with { Dashes = value });
        Option(rows, "autocorrect.ordinal", options => options.OrdinalSuperscript, (options, value) => options with { OrdinalSuperscript = value });
        Option(rows, "autocorrect.lists", options => options.AutomaticLists, (options, value) => options with { AutomaticLists = value });
        Option(rows, "autocorrect.borders", options => options.ApplyBorders, (options, value) => options with { ApplyBorders = value });
        Option(rows, "autocorrect.completion", options => options.WordCompletion, (options, value) => options with { WordCompletion = value });

        foreach (var row in _dependent) row.IsEnabled = draft.Enabled;

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = rows,
        };
    }

    private void Option(
        StackPanel rows,
        string key,
        Func<AutoCorrectOptions, bool> read,
        Func<AutoCorrectOptions, bool, AutoCorrectOptions> write)
    {
        var row = Row(key, read(draft.Options), value => draft.Options = write(draft.Options, value), master: false);
        _dependent.Add(row);
        rows.Children.Add(row);
    }

    private Control Row(string key, bool value, Action<bool> changed, bool master)
    {
        var metrics = Chrome.Metrics;
        var toggle = new SwitchToggle { IsOn = value, VerticalAlignment = VerticalAlignment.Center };
        toggle.Toggled += (_, _) => changed(toggle.IsOn);
        Avalonia.Automation.AutomationProperties.SetName(toggle, Chrome.Texts[key]);

        var title = new TextBlock
        {
            Text = Chrome.Texts[key],
            FontWeight = master ? FontWeight.SemiBold : FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        var labels = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(title);
        labels.Children.Add(Chrome.Description(Chrome.Texts[key + ".description"]));

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, metrics.SpacingMedium),
        };
        Grid.SetColumn(toggle, 1);
        toggle.Margin = new Thickness(metrics.SpacingLarge, 0, 0, 0);
        grid.Children.Add(labels);
        grid.Children.Add(toggle);

        return new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, metrics.SpacingSmall, 0),
            Child = grid,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
    }
}

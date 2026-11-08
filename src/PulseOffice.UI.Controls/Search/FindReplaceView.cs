using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Search;

public sealed record FindOption(string Key, string LabelKey, bool Initial = false);

public sealed record FindRequest(string Query, string Replacement, IReadOnlySet<string> Options)
{
    public bool Has(string option) => Options.Contains(option);
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
}

public sealed class FindReplaceView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly IFindReplaceTarget _target;
    private readonly TextBox _query;
    private readonly TextBox _replacement;
    private readonly IReadOnlyList<(FindOption Option, CheckBox Box)> _options;
    private readonly TextBlock _summary;

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

        var options = new StackPanel { Spacing = metrics.SpacingSmall / 2 };
        foreach (var (_, box) in _options) options.Children.Add(box);

        Content = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            MinWidth = 420,
            Children = { _query, _replacement, options, _summary },
        };

        var footer = new DialogFooter(InterfaceDensity.Compact)
            .WithCancel(texts["find.close"], () => Completed?.Invoke(this, null));
        if (target.SupportsFindNext) footer.WithAction(texts["find.next"], FindNext);
        footer.WithAction(texts["find.replaceAll"], ReplaceAll, out var replaceAll, primary: !target.SupportsFindNext);
        replaceAll.IsEnabled = !target.IsReadOnly;
        Footer = footer;

        _query.TextChanged += (_, _) => Summarize();
        foreach (var (_, box) in _options) box.IsCheckedChanged += (_, _) => Summarize();
        Summarize();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    private FindRequest Request() => new(
        _query.Text ?? string.Empty,
        _replacement.Text ?? string.Empty,
        _options.Where(pair => pair.Box.IsChecked == true).Select(pair => pair.Option.Key).ToHashSet());

    private void Summarize()
    {
        if (_target.Summarize(Request()) is { } summary) _summary.Text = summary;
    }

    private void FindNext()
    {
        var request = Request();
        if (request.Query.Length == 0) return;
        _summary.Text = _target.FindNext(request);
    }

    private void ReplaceAll()
    {
        var request = Request();
        if (request.Query.Length == 0 || _target.IsReadOnly) return;
        _summary.Text = _target.ReplaceAll(request);
    }
}

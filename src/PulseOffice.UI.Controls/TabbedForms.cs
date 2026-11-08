using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls;

public sealed record FormTab(string Title, string Explanation, IReadOnlyList<FormField> Fields)
{
    public Func<Control>? Preview { get; init; }

    public Func<IFormValuesFeed, Control>? LivePreview { get; init; }
}

public interface IFormValuesFeed
{
    IReadOnlyDictionary<string, object> Current { get; }

    event EventHandler? Changed;
}

public sealed class FormValuesFeed : IFormValuesFeed
{
    private readonly List<FormPromptView> _panels = [];

    public IReadOnlyDictionary<string, object> Current =>
        _panels.SelectMany(panel => panel.Values).GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Last().Value);

    public event EventHandler? Changed;

    public void Attach(FormPromptView panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        _panels.Add(panel);
        panel.ValuesChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }
}

public interface ITabbedForms
{
    Task<IReadOnlyDictionary<string, object>?> AskAsync(string titleKey, IReadOnlyList<FormTab> tabs, CancellationToken ct, int initialTab = 0, double width = 620);
}

public sealed class TabbedForms(IDialogService dialogs, ITextCatalog texts) : ITabbedForms
{
    public async Task<IReadOnlyDictionary<string, object>?> AskAsync(string titleKey, IReadOnlyList<FormTab> tabs, CancellationToken ct, int initialTab = 0, double width = 620)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        var view = Build(tabs, initialTab);
        var fitted = view.Panels.Any(panel => panel.PreferredWidth is null)
            ? width
            : view.Panels.Max(panel => panel.SuggestedWidth);
        fitted = Math.Min(Math.Max(fitted, view.Dialog.StripWidth + DialogLayoutMeasures.ChromeAllowance), DialogLayoutMeasures.MaximumDialogWidth);
        return await dialogs.ShowAsync<Dictionary<string, object>>(view.Dialog, new DialogOptions { TitleKey = titleKey, PreferredWidth = fitted }, ct).ConfigureAwait(true);
    }

    public (TabbedDialogView Dialog, IReadOnlyList<FormPromptView> Panels) Build(IReadOnlyList<FormTab> tabs, int initialTab = 0)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        var panels = tabs.Select(tab => new FormPromptView(tab.Explanation, tab.Fields, texts) { ShowButtons = false }).ToList();
        var feed = new FormValuesFeed();
        foreach (var panel in panels) feed.Attach(panel);
        for (var index = 0; index < tabs.Count; index++)
            panels[index].Preview = tabs[index].Preview?.Invoke() ?? tabs[index].LivePreview?.Invoke(feed);
        var dialog = new TabbedDialogView(
            [.. tabs.Select((tab, index) => new DialogTab(tab.Title, () => panels[index]))],
            () => new Dictionary<string, object>(feed.Current),
            texts,
            initialTab: initialTab);
        return (dialog, panels);
    }
}

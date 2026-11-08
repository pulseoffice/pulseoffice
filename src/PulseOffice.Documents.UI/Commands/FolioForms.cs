using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public interface IFolioForms
{
    Task<IReadOnlyDictionary<string, object>?> AskAsync(string titleKey, string explanationKey, IReadOnlyList<FormField> fields, CancellationToken ct, double width = 460);

    Task<IReadOnlyDictionary<string, object>?> AskAsync(
        string titleKey, string explanationKey, IReadOnlyList<FormField> fields, Func<IFormValuesFeed, Avalonia.Controls.Control> preview, CancellationToken ct, double width = 460) =>
        AskAsync(titleKey, explanationKey, fields, ct, width);
}

public sealed class FolioForms(IDialogService dialogs, ITextCatalog texts) : IFolioForms
{
    public async Task<IReadOnlyDictionary<string, object>?> AskAsync(string titleKey, string explanationKey, IReadOnlyList<FormField> fields, CancellationToken ct, double width = 460) =>
        await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts[explanationKey], fields, texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = width },
            ct).ConfigureAwait(true);

    public async Task<IReadOnlyDictionary<string, object>?> AskAsync(
        string titleKey, string explanationKey, IReadOnlyList<FormField> fields, Func<IFormValuesFeed, Avalonia.Controls.Control> preview, CancellationToken ct, double width = 460)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var view = new FormPromptView(texts[explanationKey], fields, texts);
        var feed = new FormValuesFeed();
        feed.Attach(view);
        view.Preview = preview(feed);
        return await dialogs.ShowAsync<Dictionary<string, object>>(view, new DialogOptions { TitleKey = titleKey, PreferredWidth = width }, ct).ConfigureAwait(true);
    }
}

public static class FormValues
{
    public static string Text(this IReadOnlyDictionary<string, object> values, string key) => values.TryGetValue(key, out var value) && value is string text ? text : string.Empty;

    public static double Number(this IReadOnlyDictionary<string, object> values, string key, double fallback) =>
        values.TryGetValue(key, out var value) && value is double number && double.IsFinite(number) ? number : fallback;

    public static int Choice(this IReadOnlyDictionary<string, object> values, string key, int count) =>
        values.TryGetValue(key, out var value) && value is int index && index >= 0 && index < count ? index : 0;

    public static bool Flag(this IReadOnlyDictionary<string, object> values, string key) => values.TryGetValue(key, out var value) && value is true;
}

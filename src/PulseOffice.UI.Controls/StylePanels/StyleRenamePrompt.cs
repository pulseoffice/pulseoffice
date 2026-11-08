using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.StylePanels;

public interface IStyleRenamePrompt
{
    Task<string?> AskAsync(string currentName, string explanationKey, CancellationToken ct);
}

public sealed class StyleRenamePrompt(IDialogService dialogs, ITextCatalog texts) : IStyleRenamePrompt
{
    public async Task<string?> AskAsync(string currentName, string explanationKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(currentName);
        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts[explanationKey], [new TextField("name", texts["styles.rename.name"], currentName)], texts),
            new DialogOptions { TitleKey = "styles.rename.title", PreferredWidth = 420 }, ct).ConfigureAwait(true);
        return values is not null && values.TryGetValue("name", out var value) && value is string text && text.Trim() is { Length: > 0 } chosen ? chosen : null;
    }
}

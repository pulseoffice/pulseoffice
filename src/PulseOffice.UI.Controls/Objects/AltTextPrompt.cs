using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Objects;

public sealed record AltText(string? Title, string? Description);

public interface IAltTextPrompt
{
    Task<AltText?> AskAsync(AltText current, string titleKey, CancellationToken ct);
}

public sealed class AltTextPrompt(IDialogService dialogs, ITextCatalog texts) : IAltTextPrompt
{
    public async Task<AltText?> AskAsync(AltText current, string titleKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(current);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts["dialog.altText.explanation"],
                [
                    new TextField("title", texts["dialog.altText.title"], current.Title ?? string.Empty),
                    new TextField("description", texts["dialog.altText.description"], current.Description ?? string.Empty) { Multiline = true },
                ],
                texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 480 },
            ct).ConfigureAwait(true);

        return values is null ? null : new AltText(Cleaned(values["title"]), Cleaned(values["description"]));
    }

    private string? Cleaned(object value) => value is string { Length: > 0 } text && text.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}

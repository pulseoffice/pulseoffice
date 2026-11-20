using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public enum MissingRecentAnswer { Keep, Remove }

public interface IMissingRecentPrompt
{
    Task<MissingRecentAnswer> AskAsync(RecentDocument document, CancellationToken ct = default);
}

public sealed class MissingRecentPrompt(IDialogService dialogs, ITextCatalog texts) : IMissingRecentPrompt
{
    public async Task<MissingRecentAnswer> AskAsync(RecentDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var view = new ChoicePromptView(
            texts.Format("recent.missing.question", document.Name),
            texts.Format("recent.missing.explanation", document.Path),
            [
                new PromptChoice(texts["recent.missing.remove"], MissingRecentAnswer.Remove, IsDefault: true),
                new PromptChoice(texts["recent.missing.keep"], MissingRecentAnswer.Keep, IsCancel: true),
            ],
            texts);

        var chosen = await dialogs.ShowAsync<object>(
            view,
            new DialogOptions { TitleKey = "recent.missing.heading", PreferredWidth = 460, CanCancelWithEscape = true },
            ct).ConfigureAwait(true);

        return chosen as MissingRecentAnswer? ?? MissingRecentAnswer.Keep;
    }
}

using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;

namespace PulseOffice.Remote.UI;

public sealed class DialogRemoteConflictPrompt(IDialogService dialogs, ITextCatalog texts, IIconCatalog icons) : IRemoteConflictPrompt
{
    public async Task<RemoteConflictChoice> ChooseAsync(string remotePath, CancellationToken ct = default)
    {
        var view = new ChoicePromptView(
            texts.Format("remote.conflict.question", remotePath),
            texts["remote.conflict.explanation"],
            [
                new PromptChoice(texts["remote.conflict.copy"], RemoteConflictChoice.SaveCopy, IsDefault: true)
                {
                    Description = texts["remote.conflict.copy.description"],
                    Icon = "ledger.clipboard.copy",
                },
                new PromptChoice(texts["remote.conflict.overwrite"], RemoteConflictChoice.Overwrite)
                {
                    Description = texts["remote.conflict.overwrite.description"],
                    Icon = "ui.cloud",
                },
                new PromptChoice(texts["close.cancel"], RemoteConflictChoice.Cancel, IsCancel: true),
            ],
            texts,
            icons: icons);

        var chosen = await dialogs.ShowAsync<object>(
            view,
            new DialogOptions { TitleKey = "remote.conflict.title", PreferredWidth = 520, CanCancelWithEscape = true },
            ct).ConfigureAwait(true);

        return chosen is RemoteConflictChoice choice ? choice : RemoteConflictChoice.Cancel;
    }
}

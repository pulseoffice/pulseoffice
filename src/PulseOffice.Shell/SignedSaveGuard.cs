using PulseOffice.Localization;
using PulseOffice.Security;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public interface ISignedSaveGuard
{
    Task<bool> ConfirmOverwriteAsync(string path, CancellationToken ct = default);
}

public sealed class SignedSaveGuard(IDialogService dialogs, IDocumentSignatureService signatures, ITextCatalog texts) : ISignedSaveGuard
{
    public async Task<bool> ConfirmOverwriteAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!signatures.IsSigned(path)) return true;

        var view = new ChoicePromptView(
            texts.Format("save.signed.question", Path.GetFileName(path)),
            texts["save.signed.explanation"],
            [
                new PromptChoice(texts["save.signed.proceed"], true),
                new PromptChoice(texts["close.cancel"], false, IsDefault: true, IsCancel: true),
            ],
            texts);

        var chosen = await dialogs.ShowAsync<object>(
            view,
            new DialogOptions { TitleKey = "save.signed.heading", PreferredWidth = 480, CanCancelWithEscape = true },
            ct).ConfigureAwait(true);

        return chosen is true;
    }
}

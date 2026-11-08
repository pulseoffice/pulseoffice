using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Scripting.Editor;

public interface IMacroWriteAccess
{
    bool IsGranted(ScriptLanguage language, string source);

    Task<bool> GrantAsync(ScriptLanguage language, string source, CancellationToken ct);

    bool Revoke(ScriptLanguage language, string source);
}

public sealed class MacroWriteAccess(
    IMacroFingerprints fingerprints,
    IMacroWriteGrants grants,
    IMacroFileFolder folder,
    IMacroWriteConfirmation confirmation) : IMacroWriteAccess
{
    public bool IsGranted(ScriptLanguage language, string source) =>
        grants.IsGranted(fingerprints.Of(language, source ?? string.Empty));

    public async Task<bool> GrantAsync(ScriptLanguage language, string source, CancellationToken ct)
    {
        var macro = fingerprints.Of(language, source ?? string.Empty);
        if (grants.IsGranted(macro)) return true;
        if (!await confirmation.ConfirmAsync(folder.Root, ct).ConfigureAwait(true)) return false;

        return grants.Grant(macro);
    }

    public bool Revoke(ScriptLanguage language, string source) =>
        grants.Revoke(fingerprints.Of(language, source ?? string.Empty));
}

public interface IMacroWriteConfirmation
{
    Task<bool> ConfirmAsync(string folder, CancellationToken ct);
}

public sealed class MacroWriteConfirmation(IDialogService dialogs, ITextCatalog texts) : IMacroWriteConfirmation
{
    public async Task<bool> ConfirmAsync(string folder, CancellationToken ct)
    {
        var view = new ChoicePromptView(
            texts["macro.fileWrite.question"],
            texts.Format("macro.fileWrite.explanation", folder),
            [
                new PromptChoice(texts["macro.fileWrite.accept"], true),
                new PromptChoice(texts["macro.fileWrite.decline"], false, IsDefault: true, IsCancel: true),
            ],
            texts);

        var chosen = await dialogs.ShowAsync<object>(view, new DialogOptions
        {
            TitleKey = "macro.fileWrite.title",
            PreferredWidth = 560,
            CanCancelWithEscape = true,
        }, ct).ConfigureAwait(true);

        return chosen is true;
    }
}

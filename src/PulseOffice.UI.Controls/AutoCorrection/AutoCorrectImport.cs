using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Text.Spelling;

namespace PulseOffice.UI.Controls.AutoCorrection;

public sealed class AutoCorrectImport(IFileDialogService files, IAutoCorrectListReader reader, ITextCatalog texts)
{
    private readonly string[] _extensions = [".dat"];

    public async Task<(string Text, bool Success)?> RunAsync(LanguageDraft language, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(language);

        await using var chosen = await files.OpenFileAsync(
            texts["autocorrect.import.title"],
            [new FileFilter(texts["autocorrect.import.files"], _extensions)],
            ct).ConfigureAwait(true);

        if (chosen is null) return null;

        if (reader.Read(chosen.Path) is not { } lists) return (texts["autocorrect.import.failed"], false);

        return (texts.Format("autocorrect.import.done", language.Import(lists)), true);
    }
}

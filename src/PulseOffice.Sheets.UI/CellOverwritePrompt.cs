using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public interface ICellOverwritePrompt
{
    Task<bool> ConfirmAsync(int cells, CancellationToken ct);
}

public sealed class CellOverwritePrompt(IDialogService dialogs, ITextCatalog texts) : ICellOverwritePrompt
{
    public async Task<bool> ConfirmAsync(int cells, CancellationToken ct)
    {
        var answer = await dialogs.ShowAsync<object>(
            new ChoicePromptView(
                texts.Format("cells.overwrite.question", cells),
                texts["cells.overwrite.explanation"],
                [
                    new PromptChoice(texts["cells.overwrite.confirm"], true, IsDefault: true),
                    new PromptChoice(texts["prompt.cancel"], false, IsCancel: true),
                ],
                texts),
            new DialogOptions { TitleKey = "dialog.cellsOverwrite.title", PreferredWidth = 440 },
            ct).ConfigureAwait(true);

        return answer is true;
    }
}

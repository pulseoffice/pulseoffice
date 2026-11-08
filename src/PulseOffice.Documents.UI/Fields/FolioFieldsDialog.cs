using PulseOffice.Documents.UI.Editing;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;

namespace PulseOffice.Documents.UI.Fields;

public interface IFolioFieldsDialog
{
    Task ShowAsync(FolioEditor editor, FolioFieldsStart start, CancellationToken ct);
}

public sealed class FolioFieldsDialog(IDialogService dialogs, FolioFieldsServices services, IIconCatalog icons) : IFolioFieldsDialog
{
    public async Task ShowAsync(FolioEditor editor, FolioFieldsStart start, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(start);
        var model = new FolioFieldsModel(editor, services, start);
        var titleKey = start.Editing is not null ? "command.folio.edit.field.title"
            : start.Page == FolioFieldsPage.References ? "command.folio.insert.crossReference.title"
            : "command.folio.insert.fieldDialog.title";
        await dialogs.ShowAsync<object>(new FolioFieldsView(model, icons),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 860, AccessibleDescription = services.Texts["folio.fields.explanation"] }, ct).ConfigureAwait(true);
    }
}

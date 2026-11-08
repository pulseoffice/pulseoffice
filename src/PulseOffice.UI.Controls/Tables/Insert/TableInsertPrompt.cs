using PulseOffice.Document.Tables;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;

namespace PulseOffice.UI.Controls.Tables.Insert;

public interface ITableInsertPrompt
{
    Task<TableInsertChoice?> AskAsync(TableInsertRequest request, CancellationToken ct = default);

    TableInsertChoice Quick(int rows, int columns, TableInsertLimits limits);
}

public sealed class TableInsertPrompt(
    IDialogService dialogs,
    ITableStylePresets presets,
    ITableStyleGallery names,
    IIconCatalog icons,
    ITextCatalog texts,
    Galleries.IGalleryNavigator? navigator = null) : ITableInsertPrompt
{
    public async Task<TableInsertChoice?> AskAsync(TableInsertRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var designer = new TableInsertDesigner(presets.All, request);
        return await dialogs.ShowAsync<TableInsertChoice>(
            new TableInsertView(designer, request, new TableInsertServices(names, icons, texts, navigator)),
            new DialogOptions { TitleKey = request.TitleKey, PreferredWidth = 820, AccessibleDescription = texts[request.ExplanationKey] },
            ct).ConfigureAwait(true);
    }

    public TableInsertChoice Quick(int rows, int columns, TableInsertLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        return new TableInsertChoice(Math.Clamp(rows, 1, limits.MaxRows), Math.Clamp(columns, 1, limits.MaxColumns), presets.All[0], true, true);
    }
}

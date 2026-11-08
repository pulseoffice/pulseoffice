using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerWebCommands(
    IActiveSheetContext context,
    PulseOffice.UI.Controls.Objects.IRasterImageOperations operations,
    IStatusBarService status,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.image.externalEdit") with
        {
            IconKey = "ledger.image.externalEdit",
            MenuPath = "format/image",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject?.Content is RasterImageObject,
            Execute = (_, ct) => EditExternallyAsync(ct),
        },
    ];

    private async Task EditExternallyAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.SelectedObject is not { Content: RasterImageObject raster } placed) return;

        await operations.ExternalEditAsync(raster, edited => Reload(workbook, placed.Id, edited), ct).ConfigureAwait(true);
    }

    public void Reload(Workbook workbook, string objectId, RasterImageObject edited)
    {
        if (!ReferenceEquals(Context.Workbook, workbook)) return;

        var sheet = workbook.Sheets.FirstOrDefault(candidate => candidate.Objects.Any(placed => placed.Id == objectId));
        if (sheet is null) return;

        var index = sheet.Objects.FindIndex(placed => placed.Id == objectId);
        var before = sheet.Objects[index];
        if (before.Content is not RasterImageObject current) return;

        var after = before with
        {
            Content = current with
            {
                Data = edited.Data,
                PixelWidth = edited.PixelWidth,
                PixelHeight = edited.PixelHeight,
            },
        };

        void Put(PlacedObject value)
        {
            var at = sheet.Objects.FindIndex(placed => placed.Id == objectId);
            if (at >= 0) sheet.Objects[at] = value;
        }

        Put(after);

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["external.reloaded"],
            redo: () => Put(after),
            undo: () => Put(before)));

        Context.RequestRefresh();
        status.Report(texts["external.reloaded"]);
    }
}

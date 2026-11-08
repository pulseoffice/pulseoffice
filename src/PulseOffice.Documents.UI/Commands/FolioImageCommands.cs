using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Assets;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioImageCommands(
    IActiveFolioContext context,
    IFolioObjectEditing objects,
    IRasterImageOperations operations,
    IRasterImageFiles images,
    IEmbeddedFileLauncher launcher,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    IDiagnosticTexts diagnostics) : FolioCommandSet(context)
{
    private const double MinimumDpi = 150;

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. Enum.GetValues<ImageFilter>().Where(filter => filter != ImageFilter.None).Select(filter =>
            Image($"folio.image.filter{filter}", (image, _) => Task.FromResult(operations.Baked(image, filter == ImageFilter.Grayscale && image.Filter == ImageFilter.Grayscale ? ImageFilter.None : filter)))),
        Image("folio.image.crop", operations.CropAsync),
        Image("folio.image.replace", operations.ReplaceAsync),
        Image("folio.image.compress", operations.CompressAsync),
        Image("folio.image.color", (image, ct) => operations.ColorAsync(image, "command.folio.image.color.title", ct)),
        Image("folio.image.colorReplacer", (image, ct) => operations.RecolorAsync(image, "command.folio.image.colorReplacer.title", ct)),
        Image("folio.image.imageMap", (image, ct) => operations.ImageMapAsync(image, "command.folio.image.imageMap.title", ct)),
        Viewing("folio.image.save") with
        {
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            RequiredSelection = FolioObjectCommands.ObjectSelection,
            EnabledWhen = _ => Picture() is not null,
            Execute = async (_, ct) =>
            {
                if (Picture() is { } image && Editor is { SelectedObjectId: { } id }) await operations.SaveAsync(image, id, ct).ConfigureAwait(true);
            },
        },
        Editing("folio.image.externalEdit") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredSelection = FolioObjectCommands.ObjectSelection,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Picture() is not null,
            Execute = async (_, ct) =>
            {
                if (Editor is not { SelectedObjectId: { } id } editor || Picture() is not { } image) return;
                await operations.ExternalEditAsync(image, edited =>
                {
                    if (editor.Document.Objects.Find(id) is null) return;
                    editor.SelectObject(id);
                    objects.Replace(editor, _ => edited);
                }, ct).ConfigureAwait(true);
            },
        },
        Viewing("folio.image.sizeCheck") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && editor.Document.Objects.All.Any(item => item.Content is EmbeddedContent { Value: RasterImageObject }),
            Execute = (_, ct) => SizeCheckAsync(ct),
        },
        Viewing("folio.object.openPackaged") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredSelection = FolioObjectCommands.ObjectSelection,
            EnabledWhen = _ => Editor is { } editor && objects.Selected(editor) is { Content: EmbeddedContent { Value: PackagedObject } },
            Execute = async (_, ct) =>
            {
                if (Editor is not { } editor || objects.Selected(editor) is not { Content: EmbeddedContent { Value: PackagedObject packaged } }) return;
                var opened = await launcher.OpenAsync(packaged.Data, packaged.FileName, ct).ConfigureAwait(true);
                status.Report(opened.Succeeded ? texts.Format("object.opened", packaged.FileName) : diagnostics.Describe(opened.Diagnostics[0]));
            },
        },
    ];

    private RasterImageObject? Picture() =>
        Editor is { } editor && objects.Selected(editor) is { Content: EmbeddedContent { Value: RasterImageObject image } } ? image : null;

    private CommandDefinition Image(string id, Func<RasterImageObject, CancellationToken, Task<RasterImageObject?>> change) =>
        Editing(id) with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredSelection = FolioObjectCommands.ObjectSelection,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Picture() is not null,
            Execute = async (_, ct) =>
            {
                if (Editor is not { SelectedObjectId: { } id } editor || Picture() is not { } image) return;
                if (await change(image, ct).ConfigureAwait(true) is not { } changed || editor.Document.Objects.Find(id) is null) return;
                editor.SelectObject(id);
                objects.Replace(editor, _ => changed);
            },
        };

    private async Task SizeCheckAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var lines = new List<string>();
        foreach (var item in editor.Document.Objects.All)
        {
            if (item.Content is not EmbeddedContent { Value: RasterImageObject image } || images.PixelSize(image.Data.ToArray()) is not { } size) continue;
            var inches = item.Size.Width.Value / 1440.0;
            if (inches <= 0) continue;
            var dpi = size.Width / inches;
            if (dpi < MinimumDpi) lines.Add(texts.Format("folio.imageCheck.low", item.Name ?? image.Name ?? item.Id, Math.Round(dpi)));
        }
        var message = lines.Count == 0 ? texts.Format("imageCheck.ok", MinimumDpi) : string.Join("\n", [texts.Format("imageCheck.header", MinimumDpi), .. lines]);
        await dialogs.ShowAsync<object>(new FormPromptView(message, [], texts),
            new DialogOptions { TitleKey = "command.folio.image.sizeCheck.title", PreferredWidth = 520 }, ct).ConfigureAwait(true);
    }
}

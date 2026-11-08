using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerObjectFileCommands(
    IActiveSheetContext context,
    ISheetImageInsertion images,
    PulseOffice.UI.Controls.Objects.IEmbeddedFileLauncher launcher,
    IStatusBarService status,
    ITextCatalog texts,
    IDiagnosticTexts diagnostics,
    PulseOffice.UI.Controls.Objects.IMediaPlayer player,
    PulseOffice.UI.Controls.Objects.IPackagedFilePrompt packages,
    PulseOffice.UI.Controls.Objects.IRasterImageOperations rasters) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Insert("ledger.insert.objectFile", ct => InsertAsync(PackagedObjectKind.File, ct)),
        Insert("ledger.insert.media", ct => InsertAsync(PackagedObjectKind.Media, ct)),
        Insert("ledger.tools.mediaPlayer", PlayerAsync) with { MenuPath = "tools/media", ForbiddenState = DocumentState.None },

        Prototype("ledger.object.openPackaged") with
        {
            IconKey = "ledger.object.openPackaged",
            MenuPath = "insert/object",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            EnabledWhen = _ => Context.SelectedObject?.Content is PackagedObject,
            Execute = (_, ct) => OpenAsync(ct),
        },

        Prototype("ledger.image.imageMap") with
        {
            IconKey = "ledger.image.imageMap",
            MenuPath = "format/image",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject?.Content is RasterImageObject,
            Execute = (_, ct) => ImageMapAsync(ct),
        },
    ];

    private CommandDefinition Insert(string id, Func<CancellationToken, Task> run) => Prototype(id) with
    {
        IconKey = id,
        MenuPath = "insert/object",
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => Context.HasTarget,
        Execute = (_, ct) => run(ct),
    };

    private async Task InsertAsync(PackagedObjectKind kind, CancellationToken ct)
    {
        if (await packages.PickAsync(kind, Guid.NewGuid().ToString("N"), ct).ConfigureAwait(true) is not { } packaged) return;
        var chosen = packaged.FileName;

        var size = kind == PackagedObjectKind.Media
            ? new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(4.5))
            : new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2));

        if (images.InsertObject(packaged, size, texts["history.object.insert"]))
            status.Report(texts.Format("object.inserted", chosen));
    }

    private async Task PlayerAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var media = workbook.Sheets.SelectMany(sheet => sheet.Objects)
            .Select(placed => placed.Content).OfType<PackagedObject>()
            .Where(packaged => packaged.Packaged == PackagedObjectKind.Media)
            .Select(packaged => new PulseOffice.UI.Controls.Objects.MediaItem(packaged.Id, packaged.FileName, packaged.MediaType, packaged.Data))
            .ToList();

        if (await player.ShowAsync(media, "command.ledger.tools.mediaPlayer.title", ct).ConfigureAwait(true) is not { FromFile: true } chosen) return;

        var packaged = new PackagedObject
        {
            Id = Guid.NewGuid().ToString("N"),
            Packaged = PackagedObjectKind.Media,
            FileName = chosen.Name,
            MediaType = chosen.MediaType,
            Data = chosen.Data.ToArray(),
        };
        if (images.InsertObject(packaged, new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(4.5)), texts["history.object.insert"]))
            status.Report(texts.Format("object.inserted", chosen.Name));
    }

    private async Task OpenAsync(CancellationToken ct)
    {
        if (Context.SelectedObject?.Content is not PackagedObject packaged) return;

        var opened = await launcher.OpenAsync(packaged.Data, packaged.FileName, ct).ConfigureAwait(true);

        status.Report(opened.Succeeded
            ? texts.Format("object.opened", packaged.FileName)
            : diagnostics.Describe(opened.Diagnostics[0]));
    }

    private async Task ImageMapAsync(CancellationToken ct)
    {
        if (Context.SelectedObject is not { Content: RasterImageObject raster } placed) return;

        if (await rasters.ImageMapAsync(raster, "command.ledger.image.imageMap.title", ct).ConfigureAwait(true) is not { } mapped) return;

        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);

        if (index < 0) return;

        var before = sheet.Objects[index];
        var after = before with { Content = mapped };

        sheet.Objects[index] = after;

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["command.ledger.image.imageMap.title"],
            redo: () => sheet.Objects[index] = after,
            undo: () => sheet.Objects[index] = before));

        Context.RequestRefresh();
    }
}

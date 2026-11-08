using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerImageCommands(
    IActiveSheetContext context,
    IRasterImageFiles images,
    IRasterRecolor recolor,
    IRasterColorAdjust colorAdjust,
    IDialogService dialogs,
    IStatusBarService status,
    PulseOffice.UI.Controls.Objects.IAltTextPrompt altText,
    ITextCatalog texts,
    PulseOffice.UI.Icons.IIconCatalog icons) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.object.positionSize") with
        {
            MenuPath = "format/image",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject is not null,
            Execute = (_, ct) => PositionSizeAsync(ct),
        },

        Command("ledger.image.originalSize", (_, placed, raster) =>
            placed with { Size = NaturalSize(raster) }),

        Command("ledger.image.fitToCell", (sheet, placed, _) =>
            placed with { Size = CellSize(sheet, placed), OffsetX = Twip.Zero, OffsetY = Twip.Zero }),

        Command("ledger.image.resetCrop", (_, placed, raster) =>
            placed with { Content = raster with { Crop = default } }),

        .. Enum.GetValues<ImageFilter>().Select(filter =>
            Command($"ledger.image.filter{filter}", (_, placed, raster) =>
                placed with { Content = raster with { Filter = filter } })),
    ];

    public IEnumerable<CommandDefinition> GetDialogCommands() =>
    [
        Prototype("ledger.image.save") with
        {
            MenuPath = "format/image",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            EnabledWhen = _ => Context.SelectedObject?.Content is RasterImageObject,
            Execute = (_, ct) => SaveAsync(ct),
        },

        Prototype("ledger.image.replace") with
        {
            MenuPath = "format/image",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject?.Content is RasterImageObject,
            Execute = (_, ct) => ReplaceAsync(ct),
        },

        Prototype("ledger.image.color") with
        {
            MenuPath = "format/image",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject?.Content is RasterImageObject,
            Execute = async (_, ct) =>
            {
                if (Context.SelectedObject is not { Content: RasterImageObject raster }) return;
                if (await colorAdjust.AskAsync(raster, "command.ledger.image.color.title", ct).ConfigureAwait(true) is { } adjusted)
                    ApplyRaster("command.ledger.image.color.title", _ => adjusted);
            },
        },

        Prototype("ledger.image.colorReplacer") with
        {
            MenuPath = "format/image",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject?.Content is RasterImageObject,
            Execute = (_, ct) => RecolorAsync(ct),
        },

        Prototype("ledger.image.compress") with
        {
            MenuPath = "format/image",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject?.Content is RasterImageObject,
            Execute = (_, ct) => CompressAsync(ct),
        },

        Prototype("ledger.image.altText") with
        {
            MenuPath = "format/image",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject is not null,
            Execute = (_, ct) => AltTextAsync(ct),
        },

        Prototype("ledger.image.crop") with
        {
            MenuPath = "format/image",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject?.Content is RasterImageObject,
            Execute = (_, ct) => CropAsync(ct),
        },
    ];

    private CommandDefinition Command(
        string id, Func<Worksheet, PlacedObject, RasterImageObject, PlacedObject> change) =>
        Prototype(id) with
        {
            MenuPath = "format/image",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject?.Content is RasterImageObject,
            Execute = (_, _) =>
            {
                Apply(id, change);
                return Task.CompletedTask;
            },
        };

    private void Apply(string id, Func<Worksheet, PlacedObject, RasterImageObject, PlacedObject> change)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.SelectedObject is not { Content: RasterImageObject raster } placed) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);
        if (index < 0) return;

        var updated = change(sheet, placed, raster);

        Replace(sheet, index, placed, updated, texts[$"command.{id}.title"]);
    }

    private readonly ObjectAnchoring[] _anchorings = [ObjectAnchoring.MoveAndSize, ObjectAnchoring.Move, ObjectAnchoring.Fixed];

    private async Task PositionSizeAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook || Context.SelectedObject is not { } placed) return;
        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);
        if (index < 0) return;
        var view = new FormPromptView(texts["ledger.object.positionSize.explanation"],
        [
            new PulseOffice.UI.Controls.Fields.SizeLockField("size", texts["size.title"],
                new PulseOffice.UI.Controls.Fields.LockedSize(Math.Round(placed.Size.Width.ToCentimeters(), 2), Math.Round(placed.Size.Height.ToCentimeters(), 2), placed.Content is RasterImageObject),
                "cm", 0.05, 500, 0.1, icons),
            new NumberField("x", texts["ledger.object.offsetX"], Math.Round(placed.OffsetX.ToCentimeters(), 2), 0, 500, 0.1) { Unit = "cm" },
            new NumberField("y", texts["ledger.object.offsetY"], Math.Round(placed.OffsetY.ToCentimeters(), 2), 0, 500, 0.1) { Unit = "cm" },
            new ChoiceField("anchoring", texts["ledger.object.anchoring"],
                [.. _anchorings.Select(mode => texts[$"ledger.object.anchoring.{mode.ToString().ToLowerInvariant()}"])],
                Math.Max(0, Array.IndexOf(_anchorings, placed.Anchoring))),
        ], texts);
        if (await dialogs.ShowAsync<Dictionary<string, object>>(view, new DialogOptions { TitleKey = "command.ledger.object.positionSize.title" }, ct).ConfigureAwait(true) is not { } values) return;
        var size = values["size"] is PulseOffice.UI.Controls.Fields.LockedSize locked
            ? new DocumentSize(Twip.FromCentimeters(locked.Width), Twip.FromCentimeters(locked.Height))
            : placed.Size;
        var updated = placed with
        {
            Size = size,
            OffsetX = Twip.FromCentimeters(Convert.ToDouble(values["x"], System.Globalization.CultureInfo.InvariantCulture)),
            OffsetY = Twip.FromCentimeters(Convert.ToDouble(values["y"], System.Globalization.CultureInfo.InvariantCulture)),
            Anchoring = values.TryGetValue("anchoring", out var chosen) && chosen is int mode && mode >= 0 && mode < _anchorings.Length
                ? _anchorings[mode]
                : placed.Anchoring,
        };
        if (updated == placed) return;
        Replace(sheet, index, placed, updated, texts["command.ledger.object.positionSize.title"]);
    }

    private void Replace(
        Worksheet sheet, int index, PlacedObject previous, PlacedObject updated, string description)
    {
        sheet.Objects[index] = updated;

        Context.History?.Push(new DelegateAction(
            description,
            redo: () => sheet.Objects[index] = updated,
            undo: () => sheet.Objects[index] = previous));

        Context.SelectObject(updated);
        Context.RequestRefresh();
    }

    private static DocumentSize NaturalSize(RasterImageObject raster) =>
        raster.PixelWidth > 0 && raster.PixelHeight > 0
            ? new DocumentSize(
                Twip.FromPoints(raster.PixelWidth * 72.0 / 96),
                Twip.FromPoints(raster.PixelHeight * 72.0 / 96))
            : raster.NaturalSize;

    private static DocumentSize CellSize(Worksheet sheet, PlacedObject placed) =>
        new(sheet.Layout.ColumnWidth(placed.Anchor.Column), sheet.Layout.RowHeight(placed.Anchor.Row));

    private async Task SaveAsync(CancellationToken ct)
    {
        if (Context.SelectedObject is not { Content: RasterImageObject raster } placed) return;

        if (await images.SaveAsync(raster, placed.Id, ct).ConfigureAwait(true) is { } name)
            status.Report(texts.Format("status.image.saved", name));
    }

    private async Task ReplaceAsync(CancellationToken ct)
    {
        if (await images.PickAsync(ct).ConfigureAwait(true) is not { } source) return;
        ApplyRaster("command.ledger.image.replace.title", raster => images.Replace(raster, source));
    }

    private async Task RecolorAsync(CancellationToken ct)
    {
        if (Context.SelectedObject is not { Content: RasterImageObject raster }) return;
        if (await recolor.AskAsync(raster, "command.ledger.image.colorReplacer.title", ct).ConfigureAwait(true) is not { } result) return;

        if (result.Replaced > 0) ApplyRaster("command.ledger.image.colorReplacer.title", _ => result.Image);
        status.Report(texts.Format("colorReplacer.done", result.Replaced), NotificationSeverity.Success);
    }

    private async Task CompressAsync(CancellationToken ct)
    {
        if (Context.SelectedObject is not { Content: RasterImageObject raster }) return;
        if (await images.CompressAsync(raster, ct).ConfigureAwait(true) is not { } result) return;

        if (!result.Changed)
        {
            status.Report(texts["status.image.notCompressed"], NotificationSeverity.Warning);
            return;
        }

        ApplyRaster("command.ledger.image.compress.title", _ => result.Image);
        status.Report(texts.Format("status.image.compressed", result.BytesBefore / 1024, result.BytesAfter / 1024), NotificationSeverity.Success);
    }

    private void ApplyRaster(string titleKey, Func<RasterImageObject, RasterImageObject> change)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.SelectedObject is not { Content: RasterImageObject raster } placed) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);
        if (index < 0) return;

        Replace(sheet, index, placed, placed with { Content = change(raster) }, texts[titleKey]);
    }

    private async Task AltTextAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.SelectedObject is not { } placed) return;

        var answer = await altText.AskAsync(
            new PulseOffice.UI.Controls.Objects.AltText(placed.Content.Title, placed.Content.Description),
            "command.ledger.image.altText.title", ct).ConfigureAwait(true);
        if (answer is null) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);
        if (index < 0) return;

        Replace(
            sheet, index, placed,
            placed with
            {
                Content = placed.Content with
                {
                    Title = answer.Title,
                    Description = answer.Description,
                },
            },
            texts["command.ledger.image.altText.title"]);
    }

    private async Task CropAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.SelectedObject is not { Content: RasterImageObject raster } placed) return;

        var form = new FormPromptView(
            texts["dialog.crop.explanation"],
            [
                new NumberField("left", texts["dialog.crop.left"], raster.Crop.Left * 100, 0, 49),
                new NumberField("right", texts["dialog.crop.right"], raster.Crop.Right * 100, 0, 49),
                new NumberField("top", texts["dialog.crop.top"], raster.Crop.Top * 100, 0, 49),
                new NumberField("bottom", texts["dialog.crop.bottom"], raster.Crop.Bottom * 100, 0, 49),
            ],
            texts);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            form, new DialogOptions { TitleKey = "dialog.crop.heading" }, ct).ConfigureAwait(true);

        if (values is null) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);
        if (index < 0) return;

        Replace(
            sheet, index, placed,
            placed with
            {
                Content = raster with
                {
                    Crop = (
                        Fraction(values["left"]), Fraction(values["top"]),
                        Fraction(values["right"]), Fraction(values["bottom"])),
                },
            },
            texts["command.ledger.image.crop.title"]);
    }

    private static double Fraction(object value) =>
        Math.Clamp(Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture) / 100, 0, 0.49);
}

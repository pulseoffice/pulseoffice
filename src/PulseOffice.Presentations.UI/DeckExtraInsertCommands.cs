using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Presentations.UI;

public sealed class DeckExtraInsertCommands(
    IActiveDeckContext context,
    IAssetLibrary assets,
    IFileDialogService files,
    IRasterImageFiles images,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    Design.PhotoAlbumServices album,
    IChosenFileReader reader) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("deck.insert.gallery", GalleryAsync),
        Command("deck.insert.photoAlbum", PhotoAlbumAsync),
        Command("deck.guide.insert", GuideAsync, edits: false),
    ];

    private CommandDefinition Command(string id, Func<CancellationToken, Task> run, bool edits = true) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = edits ? DocumentState.ReadOnly : DocumentState.None,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => run(ct),
        };

    private async Task GalleryAsync(CancellationToken ct)
    {
        var chosen = await dialogs.ShowAsync<VectorAsset>(
            new AssetPickerView(assets, texts),
            new DialogOptions { TitleKey = "dialog.assetPicker.title", PreferredWidth = 620 }, ct).ConfigureAwait(true);
        if (chosen is null || Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;

        var content = new VectorGraphicObject
        {
            Id = Guid.NewGuid().ToString("N"),
            Asset = chosen,
            RecolorHex = chosen.IsRecolorable ? "#334155" : null,
        };
        var side = Twip.FromCentimeters(4);
        var element = new ObjectElement
        {
            Id = content.Id,
            Bounds = new DocumentRect(new Twip((t.Deck.Size.Width.Value - side.Value) / 2), new Twip((t.Deck.Size.Height.Value - side.Value) / 2), side, side),
            ZOrder = slide.Elements.Count,
            Content = content,
        };
        t.Actions.InsertObject(slide, element);
        t.Select([element]);
    }

    private async Task PhotoAlbumAsync(CancellationToken ct)
    {
        var picked = await files.OpenFilesAsync(texts["deck.album.pick"],
            [new FileFilter(texts["file.image"], [".png", ".jpg", ".jpeg", ".gif", ".bmp"])], ct).ConfigureAwait(true);
        if (picked.Count == 0) return;

        var photos = new List<Design.AlbumPhoto>();
        var unreadable = new List<string>();
        var tooLarge = new List<string>();
        foreach (var file in picked)
        {
            var content = await reader.ReadAsync(file, reader.ImageLimit, ct).ConfigureAwait(true);
            if (content.Status == ChosenFileReadStatus.TooLarge) tooLarge.Add(file.Name);
            else if (content.Succeeded && images.PixelSize(content.Bytes) is not null) photos.Add(new Design.AlbumPhoto(content.Bytes, file.Name));
            else unreadable.Add(file.Name);
        }
        var severity = photos.Count == 0 ? NotificationSeverity.Error : NotificationSeverity.Info;
        if (tooLarge.Count > 0)
            status.Report($"{texts.Format("status.image.tooLarge", reader.ImageLimit / (1024 * 1024))}: {string.Join(", ", tooLarge)}", severity);
        if (unreadable.Count > 0)
            status.Report($"{texts["status.image.unreadable"]}: {string.Join(", ", unreadable)}", severity);
        if (photos.Count == 0 || Context.Target is not { } t) return;

        var result = await dialogs.ShowAsync<Design.PhotoAlbumResult>(
            new Design.PhotoAlbumView(photos, album),
            new DialogOptions { TitleKey = "command.deck.insert.photoAlbum.title", PreferredWidth = 720 }, ct).ConfigureAwait(true);
        if (result is null) return;

        var pages = new PhotoAlbum().Pages(t.Deck.Size, result.Photos.Count, result.PerSlide);
        var created = t.Actions.InsertAlbum(t.CurrentIndex(), pages, result.Photos.Select(photo => new RasterImageObject
        {
            Id = Guid.NewGuid().ToString("N"),
            Data = photo.Data,
            MediaType = images.MediaTypeOf(photo.Name),
            Name = Path.GetFileNameWithoutExtension(photo.Name),
        }).ToList(), result.Captions);
        status.Report(texts.Format("deck.album.done", result.Photos.Count, created), NotificationSeverity.Success);
    }

    private async Task GuideAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t) return;
        var width = t.Deck.Size.Width.ToCentimeters();
        var height = t.Deck.Size.Height.ToCentimeters();
        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["deck.guideDialog.explanation"],
            [
                new ChoiceField("kind", texts["deck.guideDialog.kind"], [texts["deck.guideDialog.horizontal"], texts["deck.guideDialog.vertical"], texts["deck.guideDialog.point"]]),
                new NumberField("x", texts["deck.guideDialog.x"], Math.Round(t.Deck.Size.Width.ToCentimeters() / 2, 2), 0, t.Deck.Size.Width.ToCentimeters()),
                new NumberField("y", texts["deck.guideDialog.y"], Math.Round(t.Deck.Size.Height.ToCentimeters() / 2, 2), 0, t.Deck.Size.Height.ToCentimeters()),
            ], texts),
            new DialogOptions { TitleKey = "command.deck.guide.insert.title", PreferredWidth = 420 }, ct).ConfigureAwait(true);
        if (values is null) return;

        var x = Twip.FromCentimeters(Math.Clamp(Convert.ToDouble(values["x"], System.Globalization.CultureInfo.InvariantCulture), 0, width)).Value;
        var y = Twip.FromCentimeters(Math.Clamp(Convert.ToDouble(values["y"], System.Globalization.CultureInfo.InvariantCulture), 0, height)).Value;
        var kind = values["kind"] is int k ? k : 0;
        if (kind != 1) t.Deck.Grid.AddGuide(new SlideGuide(true, y));
        if (kind != 0) t.Deck.Grid.AddGuide(new SlideGuide(false, x));
        t.Deck.Grid.ShowGuides = true;
        t.Deck.Raise();
    }


}

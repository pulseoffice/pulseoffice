using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;
using PulseOffice.Platform;

namespace PulseOffice.Presentations.UI;

public sealed class DeckImageCommands(
    IActiveDeckContext context,
    IDialogService dialogs,
    IRasterImageFiles images,
    IRasterImageOperations operations,
    IPictureCompression compression,
    PulseOffice.UI.Controls.Objects.IEmbeddedFileLauncher launcher,
    IStatusBarService status,
    ITextCatalog texts,
    IDiagnosticTexts diagnostics) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. Enum.GetValues<ImageFilter>().Select(filter => Filter($"deck.image.filter{filter}", filter)),
        OriginalSize(),
        Crop(),
        Image("deck.image.imageMap", ImageMapAsync),
        Image("deck.image.colorReplacer", RecolorAsync),
        Image("deck.image.replace", ReplaceAsync),
        Image("deck.image.compress", CompressAsync),
        Image("deck.image.save", SaveAsync, edits: false),
        Image("deck.image.externalEdit", ExternalEditAsync),
        Image("deck.image.color", ColorAsync),
        Image("deck.image.tone", ToneAsync),
        Prototype("deck.image.sizeCheck") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is { } t && t.Deck.Slides.Any(slide => slide.Elements.Any(e => e is ObjectElement { Content: RasterImageObject })),
            Execute = (_, ct) => SizeCheckAsync(ct),
        },
        Prototype("deck.object.openPackaged") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target?.Selected() is ObjectElement { Content: PulseOffice.Document.Embedding.PackagedObject },
            Execute = (_, ct) => OpenPackagedAsync(ct),
        },
        SaveBackground(),
    ];

    private const double MinimumDpi = 150;

    private async Task ColorAsync(CancellationToken ct) =>
        await Edit(image => operations.ColorAsync(image, "command.deck.image.color.title", ct), "command.deck.image.color.title").ConfigureAwait(true);

    private async Task ToneAsync(CancellationToken ct) =>
        await Edit(image => operations.ToneAsync(image, "command.deck.image.tone.title", ct), "command.deck.image.tone.title").ConfigureAwait(true);

    private async Task Edit(Func<RasterImageObject, Task<RasterImageObject?>> change, string labelKey)
    {
        if (Context.Target is not { } target || Selected() is not { Image: { } image, Slide: { } slide, Element: { } element }) return;
        if (await change(image).ConfigureAwait(true) is not { } changed) return;
        if (Holds(target, slide, element))
            target.Actions.EditImage(slide, element.Id, _ => changed, labelKey);
    }

    private async Task SizeCheckAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t) return;
        var lines = new List<string>();
        for (var index = 0; index < t.Deck.Slides.Count; index++)
            foreach (var element in t.Deck.Slides[index].Elements.OfType<ObjectElement>())
            {
                if (element.Content is not RasterImageObject image || images.PixelSize(image.Data.ToArray()) is not { } size) continue;
                var inches = element.Bounds.Width.Value / 1440.0;
                if (inches <= 0) continue;
                var dpi = size.Width / inches;
                if (dpi < MinimumDpi)
                    lines.Add(texts.Format("imageCheck.low", index + 1, image.Name ?? image.Id, Math.Round(dpi)));
            }

        var message = lines.Count == 0 ? texts.Format("imageCheck.ok", MinimumDpi) : string.Join("\n", [texts.Format("imageCheck.header", MinimumDpi), .. lines]);
        await dialogs.ShowAsync<object>(new FormPromptView(message, [], texts),
            new DialogOptions { TitleKey = "command.deck.image.sizeCheck.title", PreferredWidth = 520 }, ct).ConfigureAwait(true);
    }

    private async Task OpenPackagedAsync(CancellationToken ct)
    {
        if (Context.Target?.Selected() is not ObjectElement { Content: PulseOffice.Document.Embedding.PackagedObject packaged }) return;
        var opened = await launcher.OpenAsync(packaged.Data, packaged.FileName, ct).ConfigureAwait(true);
        status.Report(opened.Succeeded ? texts.Format("object.opened", packaged.FileName) : diagnostics.Describe(opened.Diagnostics[0]));
    }

    private async Task ExternalEditAsync(CancellationToken ct)
    {
        if (Selected() is not { Image: { } image, Slide: { } slide, Element: { } element }) return;
        var target = Context.Target!;
        await operations.ExternalEditAsync(image, edited => target.Actions.EditImage(slide, element.Id, _ => edited, "command.deck.image.externalEdit.title"), ct).ConfigureAwait(true);
    }

    private Task RecolorAsync(CancellationToken ct) =>
        Edit(image => operations.RecolorAsync(image, "command.deck.image.colorReplacer.title", ct), "command.deck.image.colorReplacer.title");

    private Task ImageMapAsync(CancellationToken ct) =>
        Edit(image => operations.ImageMapAsync(image, "command.deck.image.imageMap.title", ct), "command.deck.image.imageMap.title");

    private Task ReplaceAsync(CancellationToken ct) =>
        Edit(image => operations.ReplaceAsync(image, ct), "command.deck.image.replace.title");

    private async Task CompressAsync(CancellationToken ct)
    {
        if (Selected() is not { Slide: { } slide, Element: { } element, Image: { } image } || Context.Target is not { } target) return;
        var pictures = target.Deck.Slides
            .SelectMany(each => each.Elements.OfType<ObjectElement>()
                .Where(candidate => candidate.Content is RasterImageObject)
                .Select(candidate => new CompressiblePicture(Key(each, candidate), (RasterImageObject)candidate.Content, new DocumentSize(candidate.Bounds.Width, candidate.Bounds.Height))))
            .ToList();
        var selected = new CompressiblePicture(Key(slide, element), image, new DocumentSize(element.Bounds.Width, element.Bounds.Height));
        if (await compression.CompressAsync(selected, pictures, ct).ConfigureAwait(true) is not { } changed) return;
        var changes = target.Deck.Slides
            .SelectMany(each => each.Elements.OfType<ObjectElement>()
                .Where(candidate => changed.ContainsKey(Key(each, candidate)))
                .Select(candidate => (each, candidate.Id, changed[Key(each, candidate)])))
            .ToList();
        target.Actions.EditImages(changes, "command.deck.image.compress.title");
    }

    private static string Key(Slide slide, SlideElement element) => slide.Id + "/" + element.Id;

    private async Task SaveAsync(CancellationToken ct)
    {
        if (Selected() is { Element: { } element, Image: { } image }) await operations.SaveAsync(image, element.Id, ct).ConfigureAwait(true);
    }

    private CommandDefinition SaveBackground() =>
        Prototype("deck.slide.saveBackground") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target?.CurrentSlide()?.BackgroundImage is not null,
            Execute = async (_, ct) =>
            {
                if (Context.Target?.CurrentSlide() is not { BackgroundImage: { } background } slide) return;
                await operations.SaveAsync(background, slide.Id + "-background", ct).ConfigureAwait(true);
            },
        };

    private CommandDefinition Filter(string id, ImageFilter filter) =>
        Image(id, _ =>
        {
            if (Selected() is { Slide: { } slide, Element: { } element })
                Context.Target!.Actions.EditImage(slide, element.Id,
                    raster => operations.Filtered(raster, filter), $"command.{id}.title");
            return Task.CompletedTask;
        });

    private CommandDefinition OriginalSize() =>
        Image("deck.image.originalSize", _ =>
        {
            if (Selected() is { Slide: { } slide, Element: { } element, Image: { } image })
            {
                var natural = image.NaturalSize;
                Context.Target!.Actions.TransformElement(slide, element.Id,
                    new DocumentRect(element.Bounds.Left, element.Bounds.Top, natural.Width, natural.Height),
                    element.RotationDegrees);
            }
            return Task.CompletedTask;
        });

    private CommandDefinition Crop() =>
        Image("deck.image.crop", CropAsync);

    private Task CropAsync(CancellationToken ct) =>
        Edit(image => operations.CropAsync(image, ct), "command.deck.image.crop.title");

    private CommandDefinition Image(string id, Func<CancellationToken, Task> run, bool edits = true) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = edits ? DocumentState.ReadOnly : DocumentState.None,
            EnabledWhen = _ => Selected() is not null,
            Execute = (_, ct) => run(ct),
        };

    private (Slide Slide, ObjectElement Element, RasterImageObject Image)? Selected()
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return null;
        if (t.Selected() is not ObjectElement { Content: RasterImageObject image } element) return null;
        return (slide, element, image);
    }
}

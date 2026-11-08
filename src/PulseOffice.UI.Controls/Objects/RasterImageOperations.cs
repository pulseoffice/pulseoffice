using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Objects;

public interface IRasterImageOperations
{
    RasterImageObject Filtered(RasterImageObject image, ImageFilter filter);

    RasterImageObject? Baked(RasterImageObject image, ImageFilter filter);

    Task<RasterImageObject?> CropAsync(RasterImageObject image, CancellationToken ct);

    Task<RasterImageObject?> ColorAsync(RasterImageObject image, string titleKey, CancellationToken ct);

    Task<RasterImageObject?> RecolorAsync(RasterImageObject image, string titleKey, CancellationToken ct);

    Task<RasterImageObject?> ImageMapAsync(RasterImageObject image, string titleKey, CancellationToken ct);

    Task<RasterImageObject?> ReplaceAsync(RasterImageObject image, CancellationToken ct);

    Task<RasterImageObject?> CompressAsync(RasterImageObject image, CancellationToken ct);

    Task SaveAsync(RasterImageObject image, string suggestedName, CancellationToken ct);

    Task ExternalEditAsync(RasterImageObject image, Action<RasterImageObject> edited, CancellationToken ct);
}

public sealed class RasterImageOperations(
    IDialogService dialogs,
    IRasterImageFiles images,
    IRasterRecolor recolor,
    IRasterColorAdjust colorAdjust,
    IShellOpener opener,
    IExternalEditWatcher watcher,
    IStatusBarService status,
    ITextCatalog texts,
    IDiagnosticTexts diagnostics,
    ITemporaryFiles temporary,
    IBitmapPixels pixels,
    PulseOffice.Drawing.Assets.Filters.IPixelFilterCatalog filters,
    IRasterMediaTypes mediaTypes) : IRasterImageOperations
{
    private const double MaximumCrop = 0.49;

    public RasterImageObject Filtered(RasterImageObject image, ImageFilter filter) => image with { Filter = filter };

    public RasterImageObject? Baked(RasterImageObject image, ImageFilter filter)
    {
        if (filter is ImageFilter.None or ImageFilter.Grayscale) return Filtered(image, filter);
        using var decoded = pixels.Decode(image.Data.ToArray());
        if (decoded is null) return null;
        var source = pixels.Read(decoded);
        filters.For(filter).Apply(source);
        using var result = pixels.Create(source);
        return image with { Data = pixels.EncodePng(result), MediaType = "image/png", Filter = ImageFilter.None, LinkPath = null };
    }

    public async Task<RasterImageObject?> CropAsync(RasterImageObject image, CancellationToken ct)
    {
        var form = new FormPromptView(
            texts["dialog.crop.explanation"],
            [
                new NumberField("left", texts["dialog.crop.left"], image.Crop.Left * 100, 0, 49),
                new NumberField("right", texts["dialog.crop.right"], image.Crop.Right * 100, 0, 49),
                new NumberField("top", texts["dialog.crop.top"], image.Crop.Top * 100, 0, 49),
                new NumberField("bottom", texts["dialog.crop.bottom"], image.Crop.Bottom * 100, 0, 49),
            ],
            texts);
        var values = await dialogs.ShowAsync<Dictionary<string, object>>(form, new DialogOptions { TitleKey = "dialog.crop.heading" }, ct).ConfigureAwait(true);
        if (values is null) return null;
        return image with { Crop = (Fraction(values, "left"), Fraction(values, "top"), Fraction(values, "right"), Fraction(values, "bottom")) };
    }

    public Task<RasterImageObject?> ColorAsync(RasterImageObject image, string titleKey, CancellationToken ct) => colorAdjust.AskAsync(image, titleKey, ct);

    public async Task<RasterImageObject?> RecolorAsync(RasterImageObject image, string titleKey, CancellationToken ct)
    {
        if (await recolor.AskAsync(image, titleKey, ct).ConfigureAwait(true) is not { } result) return null;
        status.Report(texts.Format("colorReplacer.done", result.Replaced), NotificationSeverity.Success);
        return result.Replaced > 0 ? result.Image : null;
    }

    public async Task<RasterImageObject?> ImageMapAsync(RasterImageObject image, string titleKey, CancellationToken ct)
    {
        if (await dialogs.ShowAsync<ImageMap>(
                new ImageMapEditorView(pixels.Decode(image.Data.ToArray()), image.Map, texts),
                new DialogOptions { TitleKey = titleKey, PreferredWidth = 900 },
                ct).ConfigureAwait(true) is not { } map) return null;
        status.Report(texts.Format("imageMap.saved", map.Areas.Count));
        return image with { Map = map };
    }

    public async Task<RasterImageObject?> ReplaceAsync(RasterImageObject image, CancellationToken ct) =>
        await images.PickAsync(ct).ConfigureAwait(true) is { } source ? images.Replace(image, source) : null;

    public async Task<RasterImageObject?> CompressAsync(RasterImageObject image, CancellationToken ct)
    {
        if (await images.CompressAsync(image, ct).ConfigureAwait(true) is not { } result) return null;
        if (!result.Changed)
        {
            status.Report(texts["status.image.notCompressed"], NotificationSeverity.Warning);
            return null;
        }
        status.Report(texts.Format("status.image.compressed", result.BytesBefore / 1024, result.BytesAfter / 1024), NotificationSeverity.Success);
        return result.Image;
    }

    public async Task SaveAsync(RasterImageObject image, string suggestedName, CancellationToken ct)
    {
        if (await images.SaveAsync(image, suggestedName, ct).ConfigureAwait(true) is { } name)
            status.Report(texts.Format("status.image.saved", name));
    }

    public async Task ExternalEditAsync(RasterImageObject image, Action<RasterImageObject> edited, CancellationToken ct)
    {
        var extension = mediaTypes.ExtensionOf(image.MediaType);
        string file;
        try
        {
            file = Path.Combine(temporary.NewFolder("edit-"), $"image{extension}");
            await File.WriteAllBytesAsync(file, image.Data.ToArray(), ct).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            status.Report(texts["external.writeFailed"], NotificationSeverity.Error);
            return;
        }
        var opened = opener.OpenFile(file);
        if (!opened.Succeeded)
        {
            status.Report(diagnostics.Describe(opened.Diagnostics[0]), NotificationSeverity.Error);
            return;
        }
        status.Report(texts["external.editing"]);
        watcher.Watch(file, bytes => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (images.PixelSize(bytes) is not { } size) return;
            edited(image with { Data = bytes, PixelWidth = size.Width, PixelHeight = size.Height, NaturalSize = new DocumentSize(new Twip(size.Width * 15), new Twip(size.Height * 15)) });
        }));
    }

    private double Fraction(IReadOnlyDictionary<string, object> values, string key) =>
        values.TryGetValue(key, out var value) && value is double number ? Math.Clamp(number / 100.0, 0, MaximumCrop) : 0;
}

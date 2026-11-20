using Avalonia;
using Avalonia.Media.Imaging;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Objects;

public sealed record RasterSource(byte[] Data, string MediaType, int PixelWidth, int PixelHeight, string FileName, string? FilePath = null);

public sealed record CompressionResult(RasterImageObject Image, long BytesBefore, long BytesAfter)
{
    public bool CropDiscarded { get; init; }

    public bool Changed => BytesAfter < BytesBefore || CropDiscarded;
}

public interface IRasterImageFiles
{
    (int Width, int Height)? PixelSize(byte[] bytes);

    string MediaTypeOf(string fileName);

    Task<RasterSource?> PickAsync(CancellationToken ct);

    Task<string?> SaveAsync(RasterImageObject raster, string suggestedName, CancellationToken ct);

    RasterImageObject Replace(RasterImageObject raster, RasterSource source);

    CompressionResult Compress(RasterImageObject raster, int maximumEdge, int jpegQuality, bool discardCrop = false);

    Task<CompressionResult?> CompressAsync(RasterImageObject raster, CancellationToken ct);
}

public sealed class RasterImageFiles(IFileDialogService files, IDialogService dialogs, ITextCatalog texts, IBitmapPixels pixels, IStatusBarService status, IRasterMediaTypes mediaTypes, PulseOffice.Drawing.Assets.Gif.IGifCodec gifs, IChosenFileReader reader) : IRasterImageFiles
{
    private static readonly int[] Edges = [3840, 1920, 1280, 800, 400];

    private static readonly string[] Readable = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".webp"];

    public (int Width, int Height)? PixelSize(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        return pixels.Dimensions(bytes);
    }

    public string MediaTypeOf(string fileName) => mediaTypes.MediaTypeOf(fileName);

    public async Task<RasterSource?> PickAsync(CancellationToken ct)
    {
        await using var chosen = await files.OpenFileAsync(
            texts["dialog.insertImage.title"],
            [new FileFilter(texts["file.image"], Readable)],
            ct).ConfigureAwait(true);

        if (chosen is null) return null;

        var content = await reader.ReadAsync(chosen, reader.ImageLimit, ct).ConfigureAwait(true);
        if (content.Status == ChosenFileReadStatus.TooLarge)
        {
            status.Report(texts.Format("status.image.tooLarge", reader.ImageLimit / (1024 * 1024)), PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error);
            return null;
        }

        if (!content.Succeeded)
        {
            status.Report($"{texts["status.image.unreadable"]}: {chosen.Name}", PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error);
            return null;
        }

        var bytes = content.Bytes;

        if (PixelSize(bytes) is { } size)
            return new RasterSource(bytes, MediaTypeOf(chosen.Name), size.Width, size.Height, chosen.Name, chosen.Path);

        status.Report($"{texts["status.image.unreadable"]}: {chosen.Name}", PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error);
        return null;
    }

    public async Task<string?> SaveAsync(RasterImageObject raster, string suggestedName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(raster);

        var extension = mediaTypes.ExtensionOf(raster.MediaType);
        await using var chosen = await files.SaveFileAsync(
            texts["dialog.imageSave.title"],
            suggestedName + extension,
            [new FileFilter(texts["file.image"], [extension])],
            ct).ConfigureAwait(true);

        if (chosen is null) return null;

        await using var stream = await chosen.OpenWrite!(ct).ConfigureAwait(true);
        await stream.WriteAsync(raster.Data, ct).ConfigureAwait(true);
        return chosen.Name;
    }

    public RasterImageObject Replace(RasterImageObject raster, RasterSource source)
    {
        ArgumentNullException.ThrowIfNull(raster);
        ArgumentNullException.ThrowIfNull(source);

        return raster with
        {
            Data = source.Data,
            MediaType = source.MediaType,
            PixelWidth = source.PixelWidth,
            PixelHeight = source.PixelHeight,
            Crop = default,
        };
    }

    public CompressionResult Compress(RasterImageObject raster, int maximumEdge, int jpegQuality, bool discardCrop = false)
    {
        ArgumentNullException.ThrowIfNull(raster);

        var before = raster.Data.Length;
        var unchanged = new CompressionResult(raster, before, before);
        if (raster.MediaType == "image/gif" && gifs.Decode(raster.Data.Span) is { Frames.Count: > 1 }) return unchanged;
        var cropping = discardCrop && raster.Crop != default;

        try
        {
            using var decoded = pixels.Decode(raster.Data.ToArray());
            if (decoded is null) return unchanged;
            using var cropped = cropping ? pixels.Create(Cropped(pixels.Read(decoded), raster.Crop)) : null;
            Bitmap original = cropped ?? decoded;

            var width = original.PixelSize.Width;
            var height = original.PixelSize.Height;
            var scale = Math.Min(1.0, maximumEdge / (double)Math.Max(width, height));
            var target = new PixelSize(Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));

            using var scaled = scale < 1 ? original.CreateScaledBitmap(target, BitmapInterpolationMode.HighQuality) : null;
            var photo = raster.MediaType == "image/jpeg";
            BitmapEncoderOptions encoder = photo
                ? new JpegBitmapEncoderOptions { Quality = Math.Clamp(jpegQuality, 10, 100) }
                : PngBitmapEncoderOptions.Default;

            using var output = new MemoryStream();
            (scaled ?? original).Save(output, encoder);
            var bytes = output.ToArray();

            return bytes.Length >= before && !cropping
                ? unchanged
                : new CompressionResult(
                    raster with
                    {
                        Data = bytes,
                        MediaType = photo ? "image/jpeg" : "image/png",
                        PixelWidth = target.Width,
                        PixelHeight = target.Height,
                        Crop = cropping ? default : raster.Crop,
                    },
                    before,
                    bytes.Length) { CropDiscarded = cropping };
        }
        catch (Exception failure) when (failure is ArgumentException or NotSupportedException or IOException or InvalidOperationException)
        {
            return unchanged;
        }
    }

    private BgraImage Cropped(BgraImage image, (double Left, double Top, double Right, double Bottom) crop)
    {
        var left = Math.Clamp((int)Math.Round(image.Width * Math.Max(0, crop.Left)), 0, image.Width - 1);
        var top = Math.Clamp((int)Math.Round(image.Height * Math.Max(0, crop.Top)), 0, image.Height - 1);
        var right = Math.Clamp(image.Width - (int)Math.Round(image.Width * Math.Max(0, crop.Right)), left + 1, image.Width);
        var bottom = Math.Clamp(image.Height - (int)Math.Round(image.Height * Math.Max(0, crop.Bottom)), top + 1, image.Height);
        var width = right - left;
        var height = bottom - top;
        var result = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
            Array.Copy(image.Pixels, ((top + row) * image.Width + left) * 4, result, row * width * 4, width * 4);
        return new BgraImage(result, width, height);
    }

    public async Task<CompressionResult?> CompressAsync(RasterImageObject raster, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(raster);

        var form = new FormPromptView(
            texts.Format("dialog.compress.explanation", raster.PixelWidth, raster.PixelHeight, raster.Data.Length / 1024),
            [
                new ChoiceField("edge", texts["dialog.compress.edge"], [.. Edges.Select(e => texts.Format("dialog.compress.pixels", e))], 2),
                new NumberField("quality", texts["dialog.compress.quality"], 80, 10, 100),
                .. raster.Crop != default ? new FormField[] { new CheckField("discardCrop", texts["dialog.compress.discardCrop"], true) } : [],
            ],
            texts);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            form, new DialogOptions { TitleKey = "dialog.compress.heading" }, ct).ConfigureAwait(true);

        if (values?["edge"] is not int index || index < 0 || index >= Edges.Length) return null;
        var quality = values["quality"] is double q ? (int)Math.Round(q) : 80;
        var discard = values.TryGetValue("discardCrop", out var flag) && flag is true;
        return Compress(raster, Edges[index], quality, discard);
    }
}

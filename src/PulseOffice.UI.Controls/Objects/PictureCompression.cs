using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Objects;

public sealed record CompressiblePicture(string Key, RasterImageObject Image, DocumentSize Displayed);

public sealed record CompressionChoice(int? Resolution, int Quality, bool DiscardCrop, bool AllPictures);

public sealed record CompressionBatch(IReadOnlyDictionary<string, RasterImageObject> Changed, long BytesBefore, long BytesAfter);

public interface IPictureCompression
{
    IReadOnlyList<int?> Resolutions { get; }

    Task<CompressionChoice?> AskAsync(CompressiblePicture selected, int pictureCount, CancellationToken ct);

    int MaximumEdge(CompressiblePicture picture, CompressionChoice choice);

    CompressionBatch Compress(IReadOnlyList<CompressiblePicture> pictures, CompressionChoice choice);

    Task<IReadOnlyDictionary<string, RasterImageObject>?> CompressAsync(CompressiblePicture selected, IReadOnlyList<CompressiblePicture> all, CancellationToken ct);
}

public sealed class PictureCompression(IDialogService dialogs, IRasterImageFiles images, ITextCatalog texts, IStatusBarService status) : IPictureCompression
{
    private const int DefaultResolution = 1;

    public IReadOnlyList<int?> Resolutions { get; } = [330, 220, 150, 96, null];

    public async Task<CompressionChoice?> AskAsync(CompressiblePicture selected, int pictureCount, CancellationToken ct)
    {
        var raster = selected.Image;
        var fields = new List<FormField>
        {
            new ChoiceField("resolution", texts["dialog.compress.resolution"],
                [.. Resolutions.Select(ppi => ppi is { } value ? texts.Format("dialog.compress.ppi", value) : texts["dialog.compress.keepResolution"])], DefaultResolution),
            new NumberField("quality", texts["dialog.compress.quality"], 80, 10, 100),
            new CheckField("discardCrop", texts["dialog.compress.discardCrop"], true),
        };
        if (pictureCount > 1) fields.Add(new CheckField("all", texts.Format("dialog.compress.allPictures", pictureCount), false));
        var form = new FormPromptView(
            texts.Format("dialog.compress.resolutionExplanation", raster.PixelWidth, raster.PixelHeight, raster.Data.Length / 1024, Effective(selected)),
            fields,
            texts);
        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            form, new DialogOptions { TitleKey = "dialog.compress.heading" }, ct).ConfigureAwait(true);
        if (values?["resolution"] is not int index || index < 0 || index >= Resolutions.Count) return null;
        return new CompressionChoice(
            Resolutions[index],
            values["quality"] is double quality ? (int)Math.Round(quality) : 80,
            values.TryGetValue("discardCrop", out var discard) && discard is true,
            values.TryGetValue("all", out var all) && all is true);
    }

    public int MaximumEdge(CompressiblePicture picture, CompressionChoice choice)
    {
        var raster = picture.Image;
        var (width, height) = Pixels(raster);
        if (width <= 0 || height <= 0) return int.MaxValue;
        var crop = raster.Crop;
        var visibleWidth = Math.Max(0.01, 1 - crop.Left - crop.Right);
        var visibleHeight = Math.Max(0.01, 1 - crop.Top - crop.Bottom);
        var cropping = choice.DiscardCrop && crop != default;
        var sourceWidth = cropping ? width * visibleWidth : width;
        var sourceHeight = cropping ? height * visibleHeight : height;
        var longest = (int)Math.Ceiling(Math.Max(sourceWidth, sourceHeight));
        if (choice.Resolution is not { } ppi || picture.Displayed.Width.Value <= 0 || picture.Displayed.Height.Value <= 0) return Math.Max(1, longest);
        var neededWidth = picture.Displayed.Width.ToInches() * ppi / (cropping ? 1 : visibleWidth);
        var neededHeight = picture.Displayed.Height.ToInches() * ppi / (cropping ? 1 : visibleHeight);
        var scale = Math.Min(1, Math.Max(neededWidth / sourceWidth, neededHeight / sourceHeight));
        return Math.Max(1, (int)Math.Ceiling(longest * scale - 1e-6));
    }

    public CompressionBatch Compress(IReadOnlyList<CompressiblePicture> pictures, CompressionChoice choice)
    {
        var changed = new Dictionary<string, RasterImageObject>(StringComparer.Ordinal);
        long bytesBefore = 0;
        long bytesAfter = 0;
        foreach (var picture in pictures)
        {
            var result = images.Compress(picture.Image, MaximumEdge(picture, choice), choice.Quality, choice.DiscardCrop);
            bytesBefore += result.BytesBefore;
            bytesAfter += result.Changed ? result.BytesAfter : result.BytesBefore;
            if (result.Changed) changed[picture.Key] = result.Image;
        }
        return new CompressionBatch(changed, bytesBefore, bytesAfter);
    }

    public async Task<IReadOnlyDictionary<string, RasterImageObject>?> CompressAsync(CompressiblePicture selected, IReadOnlyList<CompressiblePicture> all, CancellationToken ct)
    {
        if (await AskAsync(selected, all.Count, ct).ConfigureAwait(true) is not { } choice) return null;
        var batch = Compress(choice.AllPictures ? all : [selected], choice);
        if (batch.Changed.Count == 0)
        {
            status.Report(texts["status.image.notCompressed"], NotificationSeverity.Warning);
            return null;
        }
        status.Report(texts.Format("status.image.compressedMany", batch.Changed.Count, batch.BytesBefore / 1024, batch.BytesAfter / 1024), NotificationSeverity.Success);
        return batch.Changed;
    }

    private int Effective(CompressiblePicture picture)
    {
        var (width, _) = Pixels(picture.Image);
        var visible = Math.Max(0.01, 1 - picture.Image.Crop.Left - picture.Image.Crop.Right);
        var inches = picture.Displayed.Width.ToInches();
        return inches <= 0 ? 0 : (int)Math.Round(width * visible / inches);
    }

    private (int Width, int Height) Pixels(RasterImageObject raster) =>
        raster.PixelWidth > 0 && raster.PixelHeight > 0
            ? (raster.PixelWidth, raster.PixelHeight)
            : images.PixelSize(raster.Data.ToArray()) ?? (0, 0);
}

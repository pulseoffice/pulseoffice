using PulseOffice.Drawing.Assets;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Objects;

public interface IImageLinks
{
    IReadOnlyList<ExternalLink> Describe(IEnumerable<(string Id, string Name, RasterImageObject Image)> images);

    RasterImageObject? Update(RasterImageObject image);

    Task<RasterImageObject?> ChangeSourceAsync(RasterImageObject image, CancellationToken ct);

    RasterImageObject Break(RasterImageObject image);

    Task<ExternalLinkRequest?> ChooseAsync(IReadOnlyList<ExternalLink> links, string titleKey, CancellationToken ct);
}

public sealed class ImageLinks(IRasterImageFiles files, IDialogService dialogs, ITextCatalog texts) : IImageLinks
{
    public IReadOnlyList<ExternalLink> Describe(IEnumerable<(string Id, string Name, RasterImageObject Image)> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        return
        [
            .. images
                .Where(entry => entry.Image.LinkPath is { Length: > 0 })
                .Select(entry => new ExternalLink(entry.Id, entry.Name, entry.Image.LinkPath!, File.Exists(entry.Image.LinkPath))),
        ];
    }

    public RasterImageObject? Update(RasterImageObject image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.LinkPath is not { Length: > 0 } path || !File.Exists(path)) return null;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (files.PixelSize(bytes) is not { } size) return null;
        return files.Replace(image, new RasterSource(bytes, files.MediaTypeOf(path), size.Width, size.Height, Path.GetFileName(path), path));
    }

    public async Task<RasterImageObject?> ChangeSourceAsync(RasterImageObject image, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (await files.PickAsync(ct).ConfigureAwait(true) is not { FilePath: { Length: > 0 } path } source) return null;
        return files.Replace(image, source) with { LinkPath = path };
    }

    public RasterImageObject Break(RasterImageObject image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return image with { LinkPath = null };
    }

    public Task<ExternalLinkRequest?> ChooseAsync(IReadOnlyList<ExternalLink> links, string titleKey, CancellationToken ct) =>
        dialogs.ShowAsync<ExternalLinkRequest>(
            new ExternalLinksView(links, texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 560 },
            ct);
}

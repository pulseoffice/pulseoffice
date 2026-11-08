namespace PulseOffice.Drawing.Assets;

public interface IRasterMediaTypes
{
    string ExtensionOf(string mediaType);

    string MediaTypeOf(string fileName);
}

public sealed class RasterMediaTypes : IRasterMediaTypes
{
    private readonly IReadOnlyDictionary<string, string> _mediaTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".webp"] = "image/webp",
    };

    private readonly IReadOnlyDictionary<string, string> _extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/gif"] = ".gif",
        ["image/bmp"] = ".bmp",
        ["image/tiff"] = ".tif",
        ["image/webp"] = ".webp",
    };

    public string ExtensionOf(string mediaType) =>
        _extensions.GetValueOrDefault(mediaType ?? string.Empty, ".png");

    public string MediaTypeOf(string fileName) =>
        _mediaTypes.GetValueOrDefault(Path.GetExtension(fileName ?? string.Empty), "image/png");
}

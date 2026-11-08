using System.Text;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats;

public sealed class VectorFallback(IVectorRasterizer rasterizer, SvgRecolour recolour)
{
    private const double PixelsPerPoint = 96.0 / 72.0 * 2;
    private const int LargestSide = 2048;
    private const string EmptySvg = "<svg xmlns=\"http://www.w3.org/2000/svg\"/>";

    private static readonly byte[] TransparentPixel =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0B, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x60, 0x00, 0x02, 0x00,
        0x00, 0x05, 0x00, 0x01, 0x7A, 0x5E, 0xAB, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
        0xAE, 0x42, 0x60, 0x82,
    ];

    public VectorFallback() : this(new NoVectorRasterizer(), new SvgRecolour())
    {
    }

    public VectorFallback(IVectorRasterizer rasterizer) : this(rasterizer, new SvgRecolour())
    {
    }

    public string MediaType => "image/svg+xml";

    public string FallbackMediaType => "image/png";

    public byte[] Svg(VectorGraphicObject vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var svg = string.IsNullOrEmpty(vector.Asset.SvgContent) ? EmptySvg : recolour.Apply(vector.Asset.SvgContent, vector.RecolorHex);
        return Encoding.UTF8.GetBytes(svg);
    }

    public byte[] Png(byte[] svg, DocumentSize size)
    {
        ArgumentNullException.ThrowIfNull(svg);
        var width = Math.Max(1, size.Width.ToPoints()) * PixelsPerPoint;
        var height = Math.Max(1, size.Height.ToPoints()) * PixelsPerPoint;
        var scale = Math.Min(1, LargestSide / Math.Max(width, height));
        return rasterizer.Png(svg, (int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale)) ?? TransparentPixel;
    }

    public bool IsSvg(byte[] data, string? mediaType, string? href)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (string.Equals(mediaType, MediaType, StringComparison.OrdinalIgnoreCase)) return true;
        if (href?.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) == true) return true;
        var head = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 1024));
        return head.Contains("<svg", StringComparison.Ordinal);
    }

    public VectorGraphicObject Read(string id, byte[] svg, bool flipHorizontal, bool flipVertical) => new()
    {
        Id = id,
        Asset = new VectorAsset { Id = id, Name = id, SourceKind = VectorSourceKind.InlineSvg, SvgContent = Encoding.UTF8.GetString(svg) },
        FlipHorizontal = flipHorizontal,
        FlipVertical = flipVertical,
    };
}

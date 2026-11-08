using PulseOffice.Formats.Imaging;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Printing;

namespace PulseOffice.Formats.Pdf;

public enum PdfImageCompression
{
    Lossless,
    Jpeg,
}

public sealed record PdfImageOptions
{
    public PdfImageCompression Compression { get; init; } = PdfImageCompression.Lossless;

    public int JpegQuality { get; init; } = 90;

    public int? MaxResolution { get; init; }

    public bool DeviceIndependentOnly { get; init; }
}

public sealed record PdfImagePlacement(PageImage Image, double WidthPoints, double HeightPoints);

public interface IImageResampler
{
    PageImage Resize(PageImage image, int width, int height);
}

public sealed class BoxImageResampler : IImageResampler
{
    public PageImage Resize(PageImage image, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (width >= image.Width && height >= image.Height) return image;
        width = Math.Clamp(width, 1, image.Width);
        height = Math.Clamp(height, 1, image.Height);

        var rgb = new byte[width * height * 3];
        var source = image.Alpha is { } mask && mask.Length == image.Width * image.Height ? mask : null;
        var alpha = source is null ? null : new byte[width * height];

        for (var y = 0; y < height; y++)
        {
            var top = y * image.Height / height;
            var bottom = Math.Max(top + 1, (y + 1) * image.Height / height);
            for (var x = 0; x < width; x++)
            {
                var left = x * image.Width / width;
                var right = Math.Max(left + 1, (x + 1) * image.Width / width);
                long red = 0, green = 0, blue = 0, opacity = 0;
                var count = 0;
                for (var row = top; row < bottom; row++)
                {
                    for (var column = left; column < right; column++)
                    {
                        var pixel = row * image.Width + column;
                        red += image.Rgb[pixel * 3];
                        green += image.Rgb[pixel * 3 + 1];
                        blue += image.Rgb[pixel * 3 + 2];
                        if (source is not null) opacity += source[pixel];
                        count++;
                    }
                }

                var target = y * width + x;
                rgb[target * 3] = (byte)(red / count);
                rgb[target * 3 + 1] = (byte)(green / count);
                rgb[target * 3 + 2] = (byte)(blue / count);
                if (alpha is not null) alpha[target] = (byte)(opacity / count);
            }
        }

        return new PageImage(width, height, rgb) { Alpha = alpha };
    }
}

public interface IPdfImageWriter
{
    int Write(PdfFile file, PdfImagePlacement placement, PdfImageOptions options, bool compress, ICollection<Diagnostic> diagnostics);
}

public sealed class PdfImageWriter(IJpegEncoder jpeg, IImageResampler resampler, IJpegInspector inspector) : IPdfImageWriter
{
    private const double PointsPerInch = 72;

    public PdfImageWriter() : this(new UnavailableJpegEncoder(), new BoxImageResampler(), new JpegInspector())
    {
    }

    public int Write(PdfFile file, PdfImagePlacement placement, PdfImageOptions options, bool compress, ICollection<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (Passed(file, placement, options, compress) is { } passed) return passed;

        var image = Limit(placement, options.MaxResolution);
        var dictionary = FormattableString.Invariant(
            $"/Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8");

        if (image.Alpha is { } alpha && alpha.Length == image.Width * image.Height)
        {
            var mask = file.WriteStream(alpha, compress, FormattableString.Invariant(
                $"/Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /DeviceGray /BitsPerComponent 8"));
            dictionary += FormattableString.Invariant($" /SMask {mask} 0 R");
        }

        if (options.Compression == PdfImageCompression.Jpeg)
        {
            if (jpeg.Encode(image.Width, image.Height, image.Rgb, Math.Clamp(options.JpegQuality, 1, 100)) is { Length: > 0 } encoded)
                return file.WriteFiltered(encoded, "/DCTDecode", dictionary);

            if (!diagnostics.Any(item => item.Code == "PDF020"))
                diagnostics.Add(Diagnostic.Warning("PDF020", "JPEG compression is not available, images were written without loss."));
        }

        return file.WriteStream(image.Rgb, compress, dictionary);
    }

    private int? Passed(PdfFile file, PdfImagePlacement placement, PdfImageOptions options, bool compress)
    {
        if (placement.Image is not { Source: { } source, Alpha: null }) return null;
        if (!string.Equals(source.MediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(source.MediaType, "image/jpg", StringComparison.OrdinalIgnoreCase)) return null;
        if (inspector.Inspect(source.Data.Span) is not { } facts) return null;

        if (options.MaxResolution is > 0 && placement.WidthPoints > 0 && placement.HeightPoints > 0
            && (facts.Width > Math.Ceiling(placement.WidthPoints / PointsPerInch * options.MaxResolution.Value * 1.05)
                || facts.Height > Math.Ceiling(placement.HeightPoints / PointsPerInch * options.MaxResolution.Value * 1.05)))
            return null;

        var (device, profileSpace) = facts.Components switch
        {
            1 => ("/DeviceGray", "GRAY"),
            3 => ("/DeviceRGB", "RGB "),
            _ => ("/DeviceCMYK", "CMYK"),
        };
        var profile = facts.IccProfile is { Length: >= 128 } icc && System.Text.Encoding.ASCII.GetString(icc, 16, 4) == profileSpace ? icc : null;
        if (profile is null && facts.Components == 4 && options.DeviceIndependentOnly) return null;

        var colourSpace = profile is null
            ? device
            : FormattableString.Invariant($"[/ICCBased {file.WriteStream(profile, compress, FormattableString.Invariant($"/N {facts.Components} /Alternate {device}"))} 0 R]");
        var decode = facts.Components == 4 && facts.Adobe ? " /Decode [1 0 1 0 1 0 1 0]" : string.Empty;

        return file.WriteFiltered(source.Data.ToArray(), "/DCTDecode", FormattableString.Invariant(
            $"/Type /XObject /Subtype /Image /Width {facts.Width} /Height {facts.Height} /ColorSpace {colourSpace} /BitsPerComponent 8{decode}"));
    }

    private PageImage Limit(PdfImagePlacement placement, int? maxResolution)
    {
        if (maxResolution is not > 0 || placement.WidthPoints <= 0 || placement.HeightPoints <= 0) return placement.Image;

        var width = (int)Math.Ceiling(placement.WidthPoints / PointsPerInch * maxResolution.Value);
        var height = (int)Math.Ceiling(placement.HeightPoints / PointsPerInch * maxResolution.Value);
        return width < placement.Image.Width || height < placement.Image.Height
            ? resampler.Resize(placement.Image, Math.Min(width, placement.Image.Width), Math.Min(height, placement.Image.Height))
            : placement.Image;
    }
}

using PulseOffice.Formats.Imaging;
using SkiaSharp;

namespace PulseOffice.Formats.Rasterizing;

public sealed class SkiaJpegEncoder : IJpegEncoder
{
    public byte[]? Encode(int width, int height, byte[] rgb, int quality)
    {
        ArgumentNullException.ThrowIfNull(rgb);
        if (width <= 0 || height <= 0 || rgb.LongLength < (long)width * height * 3) return null;

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var pixels = new byte[width * height * 4];
        for (int source = 0, target = 0; target < pixels.Length; source += 3, target += 4)
        {
            pixels[target] = rgb[source];
            pixels[target + 1] = rgb[source + 1];
            pixels[target + 2] = rgb[source + 2];
            pixels[target + 3] = 255;
        }
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(quality, 1, 100));
        return data?.ToArray();
    }
}

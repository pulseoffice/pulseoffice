using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PulseOffice.Drawing.Assets.Filters;

namespace PulseOffice.UI.Controls.Objects;

public interface IBitmapPixels
{
    WriteableBitmap Edit(Bitmap source, Action<BgraImage> edit);

    Bitmap? Decode(byte[] data);

    (int Width, int Height)? Dimensions(byte[] data);

    byte[] EncodePng(Bitmap bitmap);

    WriteableBitmap Create(BgraImage image);

    BgraImage Read(Bitmap bitmap);
}

public sealed class BitmapPixels(PulseOffice.Drawing.Assets.IVectorRasterizer vectors) : IBitmapPixels
{
    private const int VectorPixels = 1024;

    public BitmapPixels() : this(new PulseOffice.Drawing.Assets.NoVectorRasterizer())
    {
    }

    public WriteableBitmap Edit(Bitmap source, Action<BgraImage> edit)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(edit);

        var size = source.PixelSize;
        var target = new WriteableBitmap(size, source.Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);

        using var frame = target.Lock();
        var length = frame.RowBytes * size.Height;
        var format = source.Format;
        var alpha = source.AlphaFormat;
        try
        {
            source.CopyPixels(new PixelRect(size), frame.Address, length, frame.RowBytes);
        }
        catch (NotSupportedException)
        {
            using var redrawn = new RenderTargetBitmap(size, source.Dpi);
            using (var context = redrawn.CreateDrawingContext())
                context.DrawImage(source, new Rect(0, 0, size.Width * 96 / source.Dpi.X, size.Height * 96 / source.Dpi.Y));
            redrawn.CopyPixels(new PixelRect(size), frame.Address, length, frame.RowBytes);
            (format, alpha) = (redrawn.Format, redrawn.AlphaFormat);
        }

        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];
        for (var row = 0; row < size.Height; row++)
            Marshal.Copy(frame.Address + row * frame.RowBytes, buffer, row * stride, stride);
        Normalise(buffer, format, alpha);
        edit(new BgraImage(buffer, size.Width, size.Height));
        for (var row = 0; row < size.Height; row++)
            Marshal.Copy(buffer, row * stride, frame.Address + row * frame.RowBytes, stride);

        return target;
    }

    private void Normalise(byte[] buffer, PixelFormat? format, AlphaFormat? alpha)
    {
        var swap = format == PixelFormat.Rgba8888;
        var premultiply = alpha == AlphaFormat.Unpremul;
        if (!swap && !premultiply) return;

        for (var i = 0; i + 3 < buffer.Length; i += 4)
        {
            if (swap) (buffer[i], buffer[i + 2]) = (buffer[i + 2], buffer[i]);
            if (!premultiply) continue;

            var opacity = buffer[i + 3] / 255.0;
            buffer[i] = (byte)Math.Round(buffer[i] * opacity);
            buffer[i + 1] = (byte)Math.Round(buffer[i + 1] * opacity);
            buffer[i + 2] = (byte)Math.Round(buffer[i + 2] * opacity);
        }
    }

    public WriteableBitmap Create(BgraImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var target = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = target.Lock();
        for (var row = 0; row < image.Height; row++)
            Marshal.Copy(image.Pixels, row * image.Width * 4, frame.Address + row * frame.RowBytes, image.Width * 4);
        return target;
    }

    public BgraImage Read(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        BgraImage? captured = null;
        using var copy = Edit(bitmap, image => captured = new BgraImage((byte[])image.Pixels.Clone(), image.Width, image.Height));
        return captured!;
    }

    private const long MaxPixels = 40_000_000;

    private readonly PulseOffice.Drawing.Assets.ImageHeader _header = new();

    public (int Width, int Height)? Dimensions(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (_header.Size(data) is { Width: > 0, Height: > 0 } declared) return declared;
        using var bitmap = Decode(data);
        return bitmap is null ? null : (bitmap.PixelSize.Width, bitmap.PixelSize.Height);
    }

    public Bitmap? Decode(byte[] data)
    {
        if (_header.MediaType(data) is null && IsVector(data))
            return vectors.Png(data, VectorPixels, VectorPixels) is { } picture ? Decode(picture) : null;
        try
        {
            using var stream = new MemoryStream(data);
            if (_header.Size(data) is { } size && (long)size.Width * size.Height > MaxPixels)
            {
                var width = (int)Math.Max(1, Math.Sqrt(MaxPixels * (double)size.Width / Math.Max(1, size.Height)));
                return Bitmap.DecodeToWidth(stream, width);
            }

            return new Bitmap(stream);
        }
        catch (Exception failure) when (failure is ArgumentException or NotSupportedException or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    private bool IsVector(byte[] data)
    {
        var head = System.Text.Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 1024));
        return head.Contains("<svg", StringComparison.Ordinal);
    }

    public byte[] EncodePng(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var output = new MemoryStream();
        bitmap.Save(output, PngBitmapEncoderOptions.Default);
        return output.ToArray();
    }
}

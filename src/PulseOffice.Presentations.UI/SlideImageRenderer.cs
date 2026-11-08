using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public readonly record struct RasterSlide(int Width, int Height, byte[] Rgb);

public interface ISlideImageRenderer
{
    RenderTargetBitmap Render(Presentation deck, Slide slide, int pixelWidth);

    byte[] RenderPng(Presentation deck, Slide slide, int pixelWidth);

    RasterSlide RenderPixels(Presentation deck, Slide slide, int pixelWidth);

    RasterSlide RenderBgra(Presentation deck, Slide slide, int pixelWidth);
}

public sealed class SlideImageRenderer(ISlideRenderer renderer, PulseOffice.UI.Controls.Objects.IBitmapPixels pixels) : ISlideImageRenderer
{
    public SlideImageRenderer(ISlideRenderer renderer) : this(renderer, new PulseOffice.UI.Controls.Objects.BitmapPixels())
    {
    }

    private static readonly SlidePalette ExportPalette = new(Colors.Transparent, Colors.Transparent);

    public RenderTargetBitmap Render(Presentation deck, Slide slide, int pixelWidth)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(slide);

        var width = Math.Max(1, pixelWidth);
        var size = deck.Size;
        var ratio = size.Width.Value == 0 ? 0.5625 : size.Height.Value / (double)size.Width.Value;
        var height = Math.Max(1, (int)Math.Round(width * ratio));

        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));

        var number = deck.IndexOf(slide) + 1;
        var chrome = new HeaderFooterResolver().Resolve(deck.HeaderFooter, slide, number, DateTime.Now, deck.MasterOf(slide).Elements, deck.TextLanguage);

        using (var context = bitmap.CreateDrawingContext())
            renderer.Render(context, slide, size, new Rect(0, 0, width, height),
                deck.ThemeOf(slide), deck.MasterOf(slide), ExportPalette, null, chrome, new SlideFieldsFactory().For(deck, slide));

        return bitmap;
    }

    public byte[] RenderPng(Presentation deck, Slide slide, int pixelWidth)
    {
        using var bitmap = Render(deck, slide, pixelWidth);
        using var buffer = new MemoryStream();
#pragma warning disable CS0618
        bitmap.Save(buffer);
#pragma warning restore CS0618
        return buffer.ToArray();
    }

    public RasterSlide RenderBgra(Presentation deck, Slide slide, int pixelWidth)
    {
        using var bitmap = Render(deck, slide, pixelWidth);
        var image = pixels.Read(bitmap);
        var (w, h, bgra) = (image.Width, image.Height, image.Pixels);

        return new RasterSlide(w, h, bgra);
    }

    public RasterSlide RenderPixels(Presentation deck, Slide slide, int pixelWidth)
    {
        var frame = RenderBgra(deck, slide, pixelWidth);
        var w = frame.Width;
        var h = frame.Height;
        var bgra = frame.Rgb;

        var rgb = new byte[w * h * 3];
        for (var i = 0; i < w * h; i++)
        {
            var s = i * 4;
            var d = i * 3;
            rgb[d] = bgra[s + 2];
            rgb[d + 1] = bgra[s + 1];
            rgb[d + 2] = bgra[s];
        }

        return new RasterSlide(w, h, rgb);
    }
}

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

    RasterSlide RenderBgra(Presentation deck, SlideFrameRequest request, int pixelWidth);
}

public sealed class SlideImageRenderer(ISlideRenderer renderer, PulseOffice.UI.Controls.Objects.IBitmapPixels pixels) : ISlideImageRenderer
{
    public SlideImageRenderer(ISlideRenderer renderer) : this(renderer, new PulseOffice.UI.Controls.Objects.BitmapPixels())
    {
    }

    private static readonly SlidePalette ExportPalette = new(Colors.Transparent, Colors.Transparent);

    private readonly TransitionStage _transitions = new();

    public RenderTargetBitmap Render(Presentation deck, Slide slide, int pixelWidth) =>
        Render(deck, new SlideFrameRequest(slide, null), pixelWidth);

    private RenderTargetBitmap Render(Presentation deck, SlideFrameRequest request, int pixelWidth)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Slide);

        var width = Math.Max(1, pixelWidth);
        var size = deck.Size;
        var ratio = size.Width.Value == 0 ? 0.5625 : size.Height.Value / (double)size.Width.Value;
        var height = Math.Max(1, (int)Math.Round(width * ratio));

        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        var area = new Rect(0, 0, width, height);

        using (var context = bitmap.CreateDrawingContext())
        {
            if (request is { Transition: { } frame, Outgoing: { } outgoing })
            {
                context.FillRectangle(Brushes.Black, area);
                var leaving = (Slide: outgoing, Visuals: request.OutgoingVisuals, State: frame.Outgoing);
                var entering = (Slide: request.Slide, Visuals: request.Visuals, State: frame.Incoming);
                var (below, above) = frame.OutgoingOnTop ? (entering, leaving) : (leaving, entering);
                Layer(context, deck, below.Slide, area, below.Visuals, below.State);
                Layer(context, deck, above.Slide, area, above.Visuals, above.State);
            }
            else
            {
                Draw(context, deck, request.Slide, area, request.Visuals);
            }
        }

        return bitmap;
    }

    private void Layer(DrawingContext context, Presentation deck, Slide slide, Rect area, Func<string, ElementVisual>? visuals, SlideLayerState state)
    {
        var size = area.Size;
        var matrix = Matrix.CreateTranslation(-size.Width / 2, -size.Height / 2)
            * Matrix.CreateScale(state.ScaleX, state.ScaleY)
            * Matrix.CreateRotation(Matrix.ToRadians(state.Rotation))
            * Matrix.CreateTranslation(size.Width / 2 + state.OffsetX * size.Width, size.Height / 2 + state.OffsetY * size.Height);
        var clip = state.Clip is { } shapes ? _transitions.Clip(shapes, size) : new RectangleGeometry(area);

        using (context.PushTransform(matrix))
        using (context.PushGeometryClip(clip))
        using (context.PushOpacity(Math.Clamp(state.Opacity, 0, 1)))
            Draw(context, deck, slide, area, visuals);
    }

    private void Draw(DrawingContext context, Presentation deck, Slide slide, Rect area, Func<string, ElementVisual>? visuals)
    {
        var number = deck.IndexOf(slide) + 1;
        var chrome = new HeaderFooterResolver().Resolve(deck.HeaderFooter, slide, number, DateTime.Now, deck.MasterOf(slide).Elements, deck.TextLanguage);
        renderer.Render(context, slide, deck.Size, area,
            deck.ThemeOf(slide), deck.MasterOf(slide), ExportPalette, visuals, chrome, new SlideFieldsFactory().For(deck, slide));
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

    public RasterSlide RenderBgra(Presentation deck, Slide slide, int pixelWidth) =>
        RenderBgra(deck, new SlideFrameRequest(slide, null), pixelWidth);

    public RasterSlide RenderBgra(Presentation deck, SlideFrameRequest request, int pixelWidth)
    {
        using var bitmap = Render(deck, request, pixelWidth);
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

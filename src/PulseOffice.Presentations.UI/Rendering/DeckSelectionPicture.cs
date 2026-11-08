using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI.Rendering;

public interface IDeckSelectionPicture
{
    byte[]? Render(Presentation deck, Slide slide, IReadOnlyList<SlideElement> elements);
}

public sealed class DeckSelectionPicture(ISlideRenderer renderer) : IDeckSelectionPicture
{
    private const double MaxPixels = 16_000_000;

    private readonly SlidePalette _palette = new(Colors.Transparent, Colors.Transparent);

    public double PixelsPerPoint { get; init; } = 2;

    public byte[]? Render(Presentation deck, Slide slide, IReadOnlyList<SlideElement> elements)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(elements);
        if (elements.Count == 0) return null;

        var ids = elements.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
        var target = new Rect(0, 0, deck.Size.Width.ToPoints() * PixelsPerPoint, deck.Size.Height.ToPoints() * PixelsPerPoint);
        var layers = renderer.Layers(slide, deck.Size, target, deck.ThemeOf(slide), deck.MasterOf(slide), _palette)
            .Where(layer => layer.Element is { } element && ids.Contains(element.Id) && slide.Elements.Contains(element))
            .ToList();
        if (layers.Count == 0) return null;

        var bounds = layers.Select(layer => layer.Bounds.Inflate(4 * PixelsPerPoint)).Aggregate((left, right) => left.Union(right)).Intersect(target);
        if (bounds.Width < 1 || bounds.Height < 1) return null;

        var reduction = Math.Min(1, Math.Sqrt(MaxPixels / (bounds.Width * bounds.Height)));
        var pixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(bounds.Width * reduction)), Math.Max(1, (int)Math.Ceiling(bounds.Height * reduction)));
        using var bitmap = new RenderTargetBitmap(pixels, new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        using (context.PushTransform(Matrix.CreateTranslation(-bounds.X, -bounds.Y) * Matrix.CreateScale(reduction, reduction)))
            foreach (var layer in layers) layer.Paint(context);

        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }
}

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Drawing.Printing;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Printing;

namespace PulseOffice.Presentations.UI.Rendering;

public interface IPrintableSlide
{
    double Ratio { get; }

    void Paint(IPageCanvas canvas, DocumentRect area);

    PrintTag? Structure => null;
}

public interface ISlidePagePainter
{
    IPrintableSlide For(Presentation deck, Slide slide);
}

public sealed class SlidePagePainter(ISlideRenderer renderer, IDrawingReplay replay, IObjectPathBuilder paths, IPageImageCapture capture, ISlideLinkAreas links, ISlideStructureTags structure) : ISlidePagePainter
{
    public SlidePagePainter(ISlideRenderer renderer, IDrawingReplay replay, IObjectPathBuilder paths, IPageImageCapture capture)
        : this(renderer, replay, paths, capture, new SlideLinkAreas(), new SlideStructureTags())
    {
    }

    private readonly SlidePalette _palette = new(Colors.Transparent, Colors.Transparent);

    public SlidePagePainter(ISlideRenderer renderer) : this(renderer, new DrawingReplay(), new ObjectPathBuilder(), new PageImageCapture())
    {
    }

    public double RasterDpi { get; init; } = 220;

    public Func<Slide, bool> Exported { get; init; } = _ => true;

    public IPrintableSlide For(Presentation deck, Slide slide)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(slide);
        return new PrintableSlide(this, deck, slide);
    }

    private void Paint(Presentation deck, Slide slide, IPageCanvas canvas, DocumentRect area)
    {
        var target = new Rect(area.Left.ToPoints(), area.Top.ToPoints(), area.Width.ToPoints(), area.Height.ToPoints());
        var number = deck.NumberOf(slide);
        var chrome = new HeaderFooterResolver().Resolve(deck.HeaderFooter, slide, number, DateTime.Now, deck.MasterOf(slide).Elements, deck.TextLanguage);
        var layers = renderer.Layers(slide, deck.Size, target, deck.ThemeOf(slide), deck.MasterOf(slide), _palette,
            null, chrome, new SlideFieldsFactory().For(deck, slide));

        var clipped = new ClippedPageCanvas(canvas, area);
        var scale = deck.Size.Width.Value == 0 ? 1 : target.Width / deck.Size.Width.Value;
        canvas.Tag(structure.SlideTag(slide));
        foreach (var tag in structure.ReadingOrder(slide)) canvas.Tag(tag);
        foreach (var layer in layers)
        {
            canvas.Tag(layer.Fill is null ? structure.For(slide, layer.Element) : null);
            PaintLayer(layer.Fill is null ? structure.Tagged(clipped, slide, layer.Element, Placement(layer.Element, target, scale)) : clipped, layer, target, scale);
        }
        canvas.Tag(null);
        canvas.MarkTarget(links.Anchor(slide), area.Top.ToPoints());
        foreach (var link in links.For(new SlideLinkPlacement(deck, slide, target, Exported)))
            canvas.MarkLink(link.Area, link.Target);
    }

    private void PaintLayer(IPageCanvas canvas, SlideLayer layer, Rect target, double scale)
    {
        if (layer.Fill is { } fill)
        {
            if (fill.A > 0) canvas.FillRectangle(Twips(layer.Bounds.Intersect(target)), new PrintColor(fill.R, fill.G, fill.B));
            return;
        }

        if (Record(layer) is not { } recording)
        {
            Rasterize(canvas, layer.Paint, layer.Bounds.Inflate(12).Intersect(target));
            return;
        }
        var parts = replay.Split(recording);

        if (layer.Element is ObjectElement { Content: var content } element && paths.IsExact(content))
        {
            var bounds = new DocumentRect(
                Twip.FromPoints(target.X + element.Bounds.Left.Value * scale), Twip.FromPoints(target.Y + element.Bounds.Top.Value * scale),
                Twip.FromPoints(element.Bounds.Width.Value * scale), Twip.FromPoints(element.Bounds.Height.Value * scale));
            foreach (var path in paths.Build(content, bounds))
                canvas.DrawPath(element.RotationDegrees == 0 ? path : path with { RotationDegrees = path.RotationDegrees + element.RotationDegrees });
        }
        else if (parts.Rest is { } rest)
        {
            var inked = rest.GetBounds().Intersect(target);
            Rasterize(canvas, rest.Draw, inked);
        }

        parts.PaintVector(canvas);
    }

    private SlideElementPlacement? Placement(SlideElement? element, Rect target, double scale) =>
        element is null
            ? null
            : new SlideElementPlacement(
                target.X + element.Bounds.Left.Value * scale, target.Y + element.Bounds.Top.Value * scale,
                element.Bounds.Width.Value * scale, element.Bounds.Height.Value * scale, scale, element.RotationDegrees);

    private PrintTag SlideTag(Slide slide) => structure.SlideTag(slide);

    private DrawingGroup? Record(SlideLayer layer)
    {
        var recording = new DrawingGroup();
        try
        {
            using var context = recording.Open();
            layer.Paint(context);
            return recording;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }

    private void Rasterize(IPageCanvas canvas, Action<DrawingContext> paint, Rect inked)
    {
        if (inked.Width <= 0 || inked.Height <= 0) return;

        var factor = RasterDpi / 72;
        var width = Math.Max(1, (int)Math.Ceiling(inked.Width * factor));
        var height = Math.Max(1, (int)Math.Ceiling(inked.Height * factor));

        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        using (context.PushTransform(Matrix.CreateTranslation(-inked.X, -inked.Y) * Matrix.CreateScale(factor, factor)))
            paint(context);

        canvas.DrawImage(capture.FromBitmap(bitmap), Twips(inked));
    }

    private DocumentRect Twips(Rect rect) =>
        new(Twip.FromPoints(rect.X), Twip.FromPoints(rect.Y), Twip.FromPoints(Math.Max(0, rect.Width)), Twip.FromPoints(Math.Max(0, rect.Height)));

    private sealed class PrintableSlide(SlidePagePainter painter, Presentation deck, Slide slide) : IPrintableSlide
    {
        public double Ratio => deck.Size.Width.Value == 0 ? 0.5625 : deck.Size.Height.Value / (double)deck.Size.Width.Value;

        public void Paint(IPageCanvas canvas, DocumentRect area) => painter.Paint(deck, slide, canvas, area);

        public PrintTag? Structure => painter.SlideTag(slide);
    }
}

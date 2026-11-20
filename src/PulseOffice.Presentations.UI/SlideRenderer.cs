using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.Data.UI;
using PulseOffice.Formulas;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.UI.Embedding;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Controls.Formulas;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public interface ISlideRenderer
{
    void Render(
        DrawingContext context, Slide slide, SlideSize size, Rect target,
        Theme theme, SlideMaster master, SlidePalette palette,
        Func<string, ElementVisual>? visuals = null, SlideChrome chrome = default, TextFields fields = default);

    IReadOnlyList<SlideLayer> Layers(
        Slide slide, SlideSize size, Rect target,
        Theme theme, SlideMaster master, SlidePalette palette,
        Func<string, ElementVisual>? visuals = null, SlideChrome chrome = default, TextFields fields = default);
}

public sealed record SlideLayer(Rect Bounds, Action<DrawingContext> Paint)
{
    public Color? Fill { get; init; }

    public SlideElement? Element { get; init; }
}

public readonly record struct SlidePalette(
    Color Placeholder, Color Border, Func<string, IReadOnlyList<PulseOffice.Text.Spelling.WordSpan>>? Proofing = null,
    Action<DrawingContext>? Underlay = null, SlideOutputQuality Quality = SlideOutputQuality.Color, SlideElement? Live = null,
    PulseOffice.UI.Controls.Text.Rich.IPaintedTextLines? Painted = null);

public enum SlideOutputQuality { Color, Grayscale, BlackAndWhite }

public sealed class SlideRenderer : ISlideRenderer
{
    private readonly IDrawingObjectRenderer _objects;
    private readonly TextElementPainter _text;
    private readonly MediaElementPainter _media = new();
    private readonly ChromePainter _chrome = new();
    private readonly IContentPainter _content;
    private readonly IShapeBrushes _brushes;
    private readonly PulseOffice.Presentations.UI.Rendering.ISlideBackdrop _backdrops;

    public SlideRenderer(IDrawingObjectRenderer objects, ITextCatalog texts) : this(objects, texts, new ShapeBrushes())
    {
    }

    public SlideRenderer(IDrawingObjectRenderer objects, ITextCatalog texts, IShapeBrushes brushes)
        : this(objects, texts, brushes, new PulseOffice.Presentations.UI.Rendering.SlideBackdrop())
    {
    }

    public SlideRenderer(
        IDrawingObjectRenderer objects, ITextCatalog texts, IShapeBrushes brushes,
        PulseOffice.Presentations.UI.Rendering.ISlideBackdrop backdrops)
    {
        _objects = objects;
        _backdrops = backdrops;
        _brushes = brushes;
        _text = new TextElementPainter(texts, new ListLabels());
        _content = new StandardContentPainters(objects, texts);
        _iteration = new TextIterationReveal();
        _ink = new PulseOffice.Drawing.Shapes.ShapeTextInk();
    }

    public void Render(
        DrawingContext context, Slide slide, SlideSize size, Rect target,
        Theme theme, SlideMaster master, SlidePalette palette,
        Func<string, ElementVisual>? visuals = null, SlideChrome chrome = default, TextFields fields = default)
    {
        var layers = Layers(slide, size, target, theme, master, palette, visuals, chrome, fields);
        layers[0].Paint(context);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(palette.Border), 1), target);

        using var clip = context.PushClip(target);
        foreach (var layer in layers.Skip(1)) layer.Paint(context);
    }

    public IReadOnlyList<SlideLayer> Layers(
        Slide slide, SlideSize size, Rect target,
        Theme theme, SlideMaster master, SlidePalette palette,
        Func<string, ElementVisual>? visuals = null, SlideChrome chrome = default, TextFields fields = default)
    {
        var scale = size.Width.Value == 0 ? 1 : target.Width / size.Width.Value;
        var layoutBackground = Color.FromUInt32(master.BackgroundOf(theme, slide));
        var background = _backdrops.Background(slide, master, theme);

        var backdrop = _backdrops.For(slide, master, theme);
        var layers = new List<SlideLayer>
        {
            new(target, context => context.FillRectangle(new SolidColorBrush(background), target)) { Fill = background },
        };

        if (_backdrops.Fill(slide, master) is { } fill && _brushes.Fill(fill, target) is { } brush)
            layers.Add(new SlideLayer(target, context => context.FillRectangle(brush, target)));
        else if (slide.BackgroundImage is { } own && _objects.CanRender(own))
            layers.Add(new SlideLayer(target, context => PaintBackground(context, own, slide.BackgroundLayout, target, scale)));
        else if (slide.Background is null && slide.ShowMasterBackground && master.BackgroundImage is { } inherited && _objects.CanRender(inherited))
            layers.Add(new SlideLayer(target, context => PaintBackground(context, inherited, master.BackgroundLayout, target, scale)));

        if (slide.ShowMasterObjects)
            foreach (var decoration in master.DecorationFor(slide))
                layers.Add(new SlideLayer(Bounds(decoration, target, scale),
                    context => PaintElement(context, decoration, target, scale, theme, palette, fields, ElementVisual.Shown, backdrop)) { Element = decoration });

        if (palette.Underlay is { } underlay) layers.Add(new SlideLayer(target, underlay));

        foreach (var element in slide.InZOrder().Where(element => !element.Hidden).Select(element => palette.Live is { } live && live.Id == element.Id ? live : element))
        {
            var visual = visuals?.Invoke(element.Id) ?? ElementVisual.Shown;
            layers.Add(new SlideLayer(Bounds(element, target, scale),
                context => PaintElement(context, element, target, scale, theme, palette, fields, visual, backdrop)) { Element = visual.IsIdentity ? element : null });
        }

        if (!chrome.IsEmpty) layers.Add(new SlideLayer(target, context => _chrome.Paint(context, chrome, target, theme, layoutBackground)));

        return layers;
    }

    private readonly PulseOffice.Drawing.Assets.ImageHeader _header = new();

    private readonly ITextIterationReveal _iteration;

    private readonly PulseOffice.Drawing.Shapes.IShapeTextInk _ink;

    private void PaintBackground(DrawingContext context, PulseOffice.Drawing.Assets.RasterImageObject image, BackgroundImageLayout layout, Rect target, double scale)
    {
        using var opacity = context.PushOpacity(Math.Clamp(layout.Opacity, 0, 1));
        PaintBackgroundImage(context, image, layout, target, scale);
    }

    private void PaintBackgroundImage(DrawingContext context, PulseOffice.Drawing.Assets.RasterImageObject image, BackgroundImageLayout layout, Rect target, double scale)
    {
        if (layout.Mode is BackgroundImageMode.Stretch)
        {
            _objects.Render(context, image, target);
            return;
        }

        var natural = _header.NaturalTwips(image.Data.Span);
        var width = layout.Width.Value > 0 ? layout.Width.Value * scale : (natural?.Width ?? 1440) * scale;
        var height = layout.Height.Value > 0 ? layout.Height.Value * scale : (natural?.Height ?? 1440) * scale;
        if (width < 1 || height < 1 || target.Width / width * (target.Height / height) > 10_000)
        {
            _objects.Render(context, image, target);
            return;
        }

        using (context.PushClip(target))
        {
            if (layout.Mode is BackgroundImageMode.Natural)
            {
                _objects.Render(context, image, new Rect(target.X + (target.Width - width) / 2, target.Y + (target.Height - height) / 2, width, height));
                return;
            }
            for (var y = target.Y; y < target.Bottom; y += height)
                for (var x = target.X; x < target.Right; x += width)
                    _objects.Render(context, image, new Rect(x, y, width, height));
        }
    }

    private Rect Bounds(SlideElement element, Rect target, double scale)
    {
        var bounds = new Rect(
            target.X + element.Bounds.Left.Value * scale,
            target.Y + element.Bounds.Top.Value * scale,
            element.Bounds.Width.Value * scale,
            element.Bounds.Height.Value * scale);
        if (element.RotationDegrees != 0)
            bounds = bounds.TransformToAABB(new ElementPlacement(bounds, scale).Rotated(element.RotationDegrees));
        return bounds;
    }

    private void PaintElement(
        DrawingContext context, SlideElement element, Rect target, double scale,
        Theme theme, SlidePalette palette, TextFields fields, ElementVisual visual, PulseOffice.UI.Controls.Shapes.ShapeSurroundings backdrop)
    {
        if (visual.Opacity <= 0.001) return;

        var bounds = new Rect(
            target.X + element.Bounds.Left.Value * scale,
            target.Y + element.Bounds.Top.Value * scale,
            element.Bounds.Width.Value * scale,
            element.Bounds.Height.Value * scale);
        var placement = new ElementPlacement(bounds, scale);

        using var opacity = visual.IsIdentity ? default : context.PushOpacity(visual.Opacity);
        using var animation = visual.IsIdentity ? default : context.PushTransform(placement.Animated(visual));
        using var rotation = element.RotationDegrees == 0 ? default : context.PushTransform(placement.Rotated(element.RotationDegrees));

        var scene = new SlideScene(context, bounds, scale, theme, palette, fields) { Backdrop = backdrop with { Language = fields.Language, Painted = palette.Painted } };
        if (visual.Reveal.IsWhole)
        {
            element.Accept(new ElementPaintVisitor(scene, _text, _media, _content));
            return;
        }

        var ink = element is ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject inked } ? _ink.Resolve(inked.Style, _brushes.Behind(inked.Style.Fill, backdrop).Hex) : null;
        var revealed = element switch
        {
            TextElement iterated => iterated with { Text = _iteration.Reveal(iterated.Text, visual.Reveal, theme) },
            ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject shape } drawn => drawn with { Content = _iteration.Reveal(shape, visual.Reveal, ink) },
            _ => element,
        };
        revealed.Accept(new ElementPaintVisitor(scene, _text, _media, _content));
        foreach (var layer in _iteration.Layers(element, visual.Reveal))
        {
            if (layer.Visual.Opacity <= 0.001) continue;
            using var unitOpacity = context.PushOpacity(layer.Visual.Opacity);
            using var unitMotion = context.PushTransform(placement.Animated(layer.Visual));
            _iteration.Layered(element, layer.Reveal, theme, ink).Accept(new ElementPaintVisitor(scene, _text, _media, _content));
        }
    }

    private readonly record struct ElementPlacement(Rect Bounds, double Scale)
    {
        public Matrix Animated(ElementVisual visual)
        {
            var dx = visual.OffsetX * Scale;
            var dy = visual.OffsetY * Scale;
            if (visual.Scale == 1) return Matrix.CreateTranslation(dx, dy);

            var centre = Bounds.Center;
            return Matrix.CreateTranslation(-centre.X, -centre.Y)
                * Matrix.CreateScale(visual.Scale, visual.Scale)
                * Matrix.CreateTranslation(centre.X + dx, centre.Y + dy);
        }

        public Matrix Rotated(double degrees)
        {
            var centre = Bounds.Center;
            return Matrix.CreateTranslation(-centre.X, -centre.Y)
                * Matrix.CreateRotation(degrees * Math.PI / 180.0)
                * Matrix.CreateTranslation(centre.X, centre.Y);
        }
    }
}

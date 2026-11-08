using Avalonia;
using Avalonia.Media.Imaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Presentations.UI;

public sealed class DeckConvertCommands(
    IActiveDeckContext context,
    IShapeConversion conversion,
    IShapeGeometry geometry,
    IDrawingObjectRenderer renderer,
    PulseOffice.UI.Controls.Objects.IBitmapPixels pixels) : DeckCommandSet(context)
{
    private const double PixelsPerTwip = 2.0 / 15;

    private const int MaxBitmapSide = 4096;

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Outline("deck.convert.curve", (outline, style) => Freeform(conversion.ToCurve(outline), style)),
        Outline("deck.convert.polygon", (outline, style) => Freeform(conversion.ToPolygon(outline), style)),
        Outline("deck.convert.contour", (outline, style) => Freeform(conversion.ToPolygon(outline) with { Filled = false },
            style with
            {
                Fill = ShapeFill.None,
                Line = style.Line.IsVisible ? style.Line : style.Line with { ColorHex = style.Fill.ColorHex, Dash = ShapeStrokeStyle.Solid },
            })),
        Command("deck.convert.metafile", element => SourceOf(element) is not null, element =>
        {
            var (outline, style) = SourceOf(element)!.Value;
            var svg = conversion.ToSvg(outline, style, element.Bounds.Width.Value / 15.0, element.Bounds.Height.Value / 15.0);
            return new VectorGraphicObject
            {
                Id = element.Content.Id,
                Name = element.Content.Name,
                Title = element.Content.Title,
                Description = element.Content.Description,
                NaturalSize = new PulseOffice.Foundation.Units.DocumentSize(element.Bounds.Width, element.Bounds.Height),
                Asset = new VectorAsset
                {
                    Id = element.Content.Id,
                    Name = element.Content.Name ?? element.Content.Id,
                    SourceKind = VectorSourceKind.InlineSvg,
                    SvgContent = svg,
                },
            };
        }),
        Command("deck.convert.3d", element => element.Content is ShapeObject, element => Extrude(element, ExtrusionKind.Extrude)),
        Command("deck.convert.lathe", element => element.Content is ShapeObject, element => Extrude(element, ExtrusionKind.Lathe)),
        Placed("deck.convert.bitmap", element => element.Content is not RasterImageObject && renderer.CanRender(element.Content), Bitmap),
    ];

    private EmbeddedObject Extrude(ObjectElement element, ExtrusionKind kind)
    {
        var shape = (ShapeObject)element.Content;
        var depth = PulseOffice.Foundation.Units.Twip.FromCentimeters(kind == ExtrusionKind.Lathe ? 0.6 : 1);
        return shape with { Style = shape.Style with { Extrusion = new ShapeExtrusion { Kind = kind, Depth = depth } } };
    }

    private (ShapeOutline Outline, ShapeStyle Style)? SourceOf(ObjectElement element) => element.Content switch
    {
        ShapeObject shape => (geometry.Outline(shape), shape.Style),
        FreeformObject freeform => (freeform.Path.ToOutline(), freeform.Style),
        _ => null,
    };

    private Func<ObjectElement, EmbeddedObject> Wrap(Func<ShapeOutline, ShapeStyle, FreeformObject> build) =>
        element =>
        {
            var (outline, style) = SourceOf(element)!.Value;
            return build(outline, style) with
            {
                Id = element.Content.Id,
                Name = element.Content.Name,
                Title = element.Content.Title,
                Description = element.Content.Description,
            };
        };

    private CommandDefinition Outline(string id, Func<ShapeOutline, ShapeStyle, FreeformObject> build) =>
        Command(id, element => SourceOf(element) is not null, Wrap(build));

    private FreeformObject Freeform(FreeformPath path, ShapeStyle style) =>
        new() { Id = string.Empty, Path = path, Style = style };

    private int Overflow(ObjectElement element)
    {
        if (SourceOf(element) is not { Style: { } style }) return 0;
        var margin = 0;
        if (style.Shadow.Enabled)
            margin = Math.Max(margin, Math.Max(Math.Abs(style.Shadow.OffsetX.Value), Math.Abs(style.Shadow.OffsetY.Value)) + style.Shadow.Blur.Value * 2);
        if (style.Glow.Enabled) margin = Math.Max(margin, style.Glow.Radius.Value * 2);
        if (style.Extrusion.Enabled) margin = Math.Max(margin, style.Extrusion.Depth.Value);
        return margin + (style.Line.IsVisible ? style.Line.Width.Value : 0);
    }

    private ObjectElement Bitmap(ObjectElement element)
    {
        var margin = Overflow(element);
        var bounds = new PulseOffice.Foundation.Units.DocumentRect(
            new PulseOffice.Foundation.Units.Twip(element.Bounds.Left.Value - margin),
            new PulseOffice.Foundation.Units.Twip(element.Bounds.Top.Value - margin),
            new PulseOffice.Foundation.Units.Twip(element.Bounds.Width.Value + margin * 2),
            new PulseOffice.Foundation.Units.Twip(element.Bounds.Height.Value + margin * 2));
        var scale = Math.Min(PixelsPerTwip, MaxBitmapSide / (double)Math.Max(1, Math.Max(bounds.Width.Value, bounds.Height.Value)));
        var width = Math.Max(1, (int)Math.Round(bounds.Width.Value * scale));
        var height = Math.Max(1, (int)Math.Round(bounds.Height.Value * scale));
        var inset = margin * scale;
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        using (var drawing = bitmap.CreateDrawingContext())
            renderer.Render(drawing, element.Content, new Rect(inset, inset, element.Bounds.Width.Value * scale, element.Bounds.Height.Value * scale));

        return element with { Bounds = bounds, Content = new RasterImageObject
        {
            Id = element.Content.Id,
            Name = element.Content.Name,
            Title = element.Content.Title,
            Description = element.Content.Description,
            Data = pixels.EncodePng(bitmap),
            MediaType = "image/png",
            PixelWidth = width,
            PixelHeight = height,
            NaturalSize = new PulseOffice.Foundation.Units.DocumentSize(bounds.Width, bounds.Height),
        } };
    }

    private CommandDefinition Command(string id, Func<ObjectElement, bool> accepts, Func<ObjectElement, EmbeddedObject> convert) =>
        Placed(id, accepts, element => element with { Content = convert(element) });

    private CommandDefinition Placed(string id, Func<ObjectElement, bool> accepts, Func<ObjectElement, ObjectElement> convert) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.Selected() is ObjectElement element && accepts(element),
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide && t.Selected() is ObjectElement element)
                {
                    var converted = convert(element);
                    t.Actions.ReplaceElement(slide, converted, $"command.{id}.title");
                    t.Select([converted]);
                }
                return Task.CompletedTask;
            },
        };
}

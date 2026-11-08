using Avalonia;
using Avalonia.Media;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Shapes.Painting;

namespace PulseOffice.UI.Controls.Shapes;

public interface IDrawingObjectRenderer
{
    bool CanRender(EmbeddedObject content);

    bool HasOwnBackground(EmbeddedObject content);

    void Render(DrawingContext context, EmbeddedObject content, Rect bounds);

    void Render(DrawingContext context, EmbeddedObject content, Rect bounds, ShapeSurroundings backdrop);
}

public sealed class DrawingObjectRenderer : IDrawingObjectRenderer
{
    private readonly DrawingPainters _painters;
    private readonly IFontworkRenderer _fontwork;

    public DrawingObjectRenderer(IShapeRenderer shapes, IVectorOutlineReader vectors)
        : this(shapes, vectors, new PulseOffice.UI.Controls.Objects.BitmapPixels())
    {
    }

    public DrawingObjectRenderer(IShapeRenderer shapes, IVectorOutlineReader vectors, PulseOffice.UI.Controls.Objects.IBitmapPixels pixels, IFontworkRenderer? fontwork = null)
    {
        _fontwork = fontwork ?? new FontworkRenderer();
        _painters = new DrawingPainters(
            shapes,
            new FreeformPainter(shapes),
            new VectorPainter(vectors),
            new RasterPainter(new PixelFilterCatalog(), pixels, new PulseOffice.Drawing.Assets.Gif.GifCodec()),
            new ConnectorPainter(new ShapeBrushes(), new ArrowGeometry()),
            new GroupPainter(),
            new ScenePainter());
    }

    public DrawingObjectRenderer() : this(new ShapeRenderer(), new VectorOutlineReader()) { }

    public bool CanRender(EmbeddedObject content) => content is IDrawingObject or PulseOffice.Drawing.Fontwork.FontworkObject;

    public bool HasOwnBackground(EmbeddedObject content) => CanRender(content);

    public void Render(DrawingContext context, EmbeddedObject content, Rect bounds) => Render(context, content, bounds, new ShapeSurroundings());

    public void Render(DrawingContext context, EmbeddedObject content, Rect bounds, ShapeSurroundings backdrop)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(content);

        if (content is PulseOffice.Drawing.Fontwork.FontworkObject fontwork) _fontwork.Render(context, fontwork, bounds);
        else if (content is IDrawingObject drawing)
            drawing.Accept(new PaintVisitor(context, bounds, _painters, this, backdrop));
    }
}

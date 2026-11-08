using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Drawing;

public interface IDrawingObject
{
    TResult Accept<TResult>(IDrawingObjectVisitor<TResult> visitor);
}

public interface IDrawingObjectVisitor<out TResult>
{
    TResult VisitShape(ShapeObject shape);

    TResult VisitFreeform(FreeformObject freeform);

    TResult VisitVector(VectorGraphicObject vector);

    TResult VisitRaster(RasterImageObject raster);

    TResult VisitGroup(ShapeGroupObject group);

    TResult VisitConnector(ConnectorObject connector);

    TResult VisitFontwork(Fontwork.FontworkObject fontwork);

    TResult VisitScene(Scenes.Scene3DObject scene);
}

public abstract class DrawingObjectVisitor<TResult> : IDrawingObjectVisitor<TResult>
{
    public virtual TResult VisitShape(ShapeObject shape) => Otherwise(shape);

    public virtual TResult VisitFreeform(FreeformObject freeform) => Otherwise(freeform);

    public virtual TResult VisitVector(VectorGraphicObject vector) => Otherwise(vector);

    public virtual TResult VisitRaster(RasterImageObject raster) => Otherwise(raster);

    public virtual TResult VisitGroup(ShapeGroupObject group) => Otherwise(group);

    public virtual TResult VisitConnector(ConnectorObject connector) => Otherwise(connector);

    public virtual TResult VisitFontwork(Fontwork.FontworkObject fontwork) => Otherwise(fontwork);

    public virtual TResult VisitScene(Scenes.Scene3DObject scene) => Otherwise(scene);

    protected abstract TResult Otherwise(EmbeddedObject content);
}

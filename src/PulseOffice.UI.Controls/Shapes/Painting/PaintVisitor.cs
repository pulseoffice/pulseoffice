using Avalonia;
using Avalonia.Media;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.UI.Controls.Shapes.Painting;

public sealed record DrawingPainters(
    IShapeRenderer Shapes,
    FreeformPainter Freeform,
    VectorPainter Vector,
    RasterPainter Raster,
    ConnectorPainter Connector,
    GroupPainter Group,
    ScenePainter Scene);

public sealed class PaintVisitor(
    DrawingContext context, Rect bounds, DrawingPainters painters, IDrawingObjectRenderer renderer, ShapeSurroundings backdrop)
    : DrawingObjectVisitor<bool>
{
    public override bool VisitShape(ShapeObject shape)
    {
        painters.Shapes.Render(context, shape, bounds, backdrop);
        return true;
    }

    public override bool VisitFreeform(FreeformObject freeform)
    {
        painters.Freeform.Paint(context, freeform, bounds);
        return true;
    }

    public override bool VisitVector(VectorGraphicObject vector)
    {
        painters.Vector.Paint(context, vector, bounds);
        return true;
    }

    public override bool VisitRaster(RasterImageObject raster)
    {
        painters.Raster.Paint(context, raster, bounds);
        return true;
    }

    public override bool VisitConnector(ConnectorObject connector)
    {
        painters.Connector.Paint(context, connector, bounds);
        return true;
    }

    public override bool VisitGroup(ShapeGroupObject group)
    {
        painters.Group.Paint(context, group, bounds, renderer, backdrop);
        return true;
    }

    public override bool VisitScene(PulseOffice.Drawing.Scenes.Scene3DObject scene)
    {
        painters.Scene.Paint(context, scene, bounds);
        return true;
    }

    protected override bool Otherwise(EmbeddedObject content) => false;
}

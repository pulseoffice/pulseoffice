using PulseOffice.Documents.Objects;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Controls.Drawing;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioDrawingTools : IDrawingToolState
{
    ConnectorKind Connector { get; }

    void ChooseConnector(ConnectorKind kind);
}

public sealed class FolioDrawingTools : IFolioDrawingTools
{
    private readonly DrawingToolState _state = new();

    public FolioDrawingTools() => _state.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);

    public DrawingTool Tool => _state.Tool;

    public bool Filled => _state.Filled;

    public bool EditingPoints => _state.EditingPoints;

    public bool HelplinesWhileMoving => _state.HelplinesWhileMoving;

    public ConnectorKind Connector { get; private set; } = ConnectorKind.Straight;

    public event EventHandler? Changed;

    public void Choose(DrawingTool tool, bool filled) => _state.Choose(tool, filled);

    public void ChooseConnector(ConnectorKind kind)
    {
        Connector = kind;
        _state.Choose(DrawingTool.Connector, false);
    }

    public void Finish() => _state.Finish();

    public void ToggleEditingPoints() => _state.ToggleEditingPoints();

    public void ToggleHelplines() => _state.ToggleHelplines();
}

public sealed record DrawnSketch(int Page, DrawingTool Tool, bool Filled, SketchResult Result);

public interface IFolioDrawingPlacement
{
    bool Place(FolioEditor editor, DrawnSketch sketch, ConnectorKind connector);
}

public sealed class FolioDrawingPlacement(IFolioObjectEditing objects, IPathNormalizer normalizer, IConnectorFramer framer) : IFolioDrawingPlacement
{
    private const double MinimumSide = 4;

    public bool Place(FolioEditor editor, DrawnSketch sketch, ConnectorKind connector)
    {
        if (editor.ReadOnly()) return false;
        var points = sketch.Result.Path.Figures.SelectMany(figure => figure.Nodes).Select(node => node.Point).ToList();
        if (points.Count < 2) return false;
        using (editor.History.BeginTransaction("draw"))
        {
            var placed = sketch.Tool switch
            {
                DrawingTool.Frame => Frame(editor, points),
                DrawingTool.Connector => Connector(editor, points[0], points[^1], connector),
                _ => Freeform(editor, sketch),
            };
            if (placed is not { } box) return false;
            return objects.Change(editor, item =>
            {
                item.Anchor = new PageAnchor(sketch.Page + 1);
                item.Wrap = WrapMode.Through;
                item.Placement = new ObjectPlacement(Twip.FromPoints(box.X), Twip.FromPoints(box.Y))
                {
                    Horizontal = HorizontalRelation.Page,
                    Vertical = VerticalRelation.Page,
                };
            });
        }
    }

    private PathBounds? Freeform(FolioEditor editor, DrawnSketch sketch)
    {
        var normalized = normalizer.ToUnitBox(sketch.Result.Path);
        var box = Sized(normalized.Bounds);
        var style = sketch.Filled ? ShapeStyle.Default : ShapeStyle.Default with { Fill = ShapeStyle.Default.Fill with { Kind = ShapeFillKind.None } };
        var content = new FreeformObject { Id = editor.Document.NextId("o"), Path = normalized.Path, Style = style, NaturalSize = Size(box) };
        return objects.Insert(editor, new ObjectInsertion(content, Size(box), ObjectAnchorKind.Paragraph, WrapMode.Through)) ? box : null;
    }

    private PathBounds? Frame(FolioEditor editor, IReadOnlyList<ShapePoint> points)
    {
        var box = Sized(new PathBounds(Math.Min(points[0].X, points[^1].X), Math.Min(points[0].Y, points[^1].Y),
            Math.Abs(points[^1].X - points[0].X), Math.Abs(points[^1].Y - points[0].Y)));
        return objects.InsertTextFrame(editor, Size(box)) ? box : null;
    }

    private PathBounds? Connector(FolioEditor editor, ShapePoint start, ShapePoint end, ConnectorKind kind)
    {
        var connector = new ConnectorObject { Id = editor.Document.NextId("o"), Shape = kind };
        if (framer.Frame(connector, new ConnectorBox(start.X, start.Y, 0, 0), new ConnectorBox(end.X, end.Y, 0, 0)) is not { } framed) return null;
        var box = Sized(new PathBounds(framed.Left, framed.Top, framed.Width, framed.Height));
        var content = connector with { Path = framed.Path, NaturalSize = Size(box) };
        return objects.Insert(editor, new ObjectInsertion(content, Size(box), ObjectAnchorKind.Paragraph, WrapMode.Through)) ? box : null;
    }

    private PathBounds Sized(PathBounds bounds) => bounds with { Width = Math.Max(MinimumSide, bounds.Width), Height = Math.Max(MinimumSide, bounds.Height) };

    private DocumentSize Size(PathBounds bounds) => new(Twip.FromPoints(bounds.Width), Twip.FromPoints(bounds.Height));
}

namespace PulseOffice.Drawing.Shapes.Geometry;

public sealed record DrawingGuide(string Name, string Operator, IReadOnlyList<string> Arguments);

public interface IPresetPathVisitor
{
    void VisitMove(PresetMove command);

    void VisitLine(PresetLine command);

    void VisitArc(PresetArc command);

    void VisitQuadratic(PresetQuadratic command);

    void VisitCubic(PresetCubic command);

    void VisitClose(PresetClose command);
}

public abstract record PresetPathCommand
{
    public abstract void Accept(IPresetPathVisitor visitor);
}

public sealed record PresetPoint(string X, string Y);

public sealed record PresetMove(PresetPoint To) : PresetPathCommand
{
    public override void Accept(IPresetPathVisitor visitor) => visitor.VisitMove(this);
}

public sealed record PresetLine(PresetPoint To) : PresetPathCommand
{
    public override void Accept(IPresetPathVisitor visitor) => visitor.VisitLine(this);
}

public sealed record PresetArc(string RadiusX, string RadiusY, string Start, string Sweep) : PresetPathCommand
{
    public override void Accept(IPresetPathVisitor visitor) => visitor.VisitArc(this);
}

public sealed record PresetQuadratic(PresetPoint Control, PresetPoint To) : PresetPathCommand
{
    public override void Accept(IPresetPathVisitor visitor) => visitor.VisitQuadratic(this);
}

public sealed record PresetCubic(PresetPoint First, PresetPoint Second, PresetPoint To) : PresetPathCommand
{
    public override void Accept(IPresetPathVisitor visitor) => visitor.VisitCubic(this);
}

public sealed record PresetClose : PresetPathCommand
{
    public override void Accept(IPresetPathVisitor visitor) => visitor.VisitClose(this);
}

public sealed record PresetPath(IReadOnlyList<PresetPathCommand> Commands)
{
    public double? Width { get; init; }

    public double? Height { get; init; }

    public bool Filled { get; init; } = true;

    public bool Stroked { get; init; } = true;

    public ShapeShade Shade { get; init; }
}

public sealed record PresetHandleAxis(string Reference, string Minimum, string Maximum);

public sealed record PresetHandle(PresetPoint Position, PresetHandleAxis? First, PresetHandleAxis? Second)
{
    public bool Polar { get; init; }
}

public sealed record PresetShapeDefinition(
    string Name,
    IReadOnlyList<DrawingGuide> Adjustments,
    IReadOnlyList<DrawingGuide> Guides,
    IReadOnlyList<PresetPath> Paths)
{
    public IReadOnlyList<PresetHandle> Handles { get; init; } = [];
}

using PulseOffice.Foundation.Units;

namespace PulseOffice.Drawing.Shapes;

public interface IDimensionLineTemplate
{
    DocumentSize Size { get; }

    ShapeObject Create(string id);
}

public sealed class DimensionLineTemplate : IDimensionLineTemplate
{
    private readonly DimensionLine _line = new()
    {
        Start = new ShapePoint(0, 1),
        End = new ShapePoint(1, 1),
        Unit = "cm",
        ShowUnit = true,
        DecimalPlaces = 2,
    };

    public DocumentSize Size => new(Twip.FromCentimeters(6), _line.LineDistance + _line.GuideOverhang);

    public ShapeObject Create(string id) => new()
    {
        Id = id,
        Shape = ShapeKind.Line,
        Style = ShapeStyle.Default with
        {
            Line = ShapeLine.Default with { StartArrow = ArrowEnd.Arrow, EndArrow = ArrowEnd.Arrow },
        },
        Dimension = _line,
    };
}

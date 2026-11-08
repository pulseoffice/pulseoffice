using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

public sealed class OoxmlVectorPaths
{
    private const int Grid = 21600;

    private readonly IVectorOutlineReader _vectors;
    private readonly OoxmlGeometry _geometry;
    private readonly IShapePathWriter _writer;

    public OoxmlVectorPaths(IVectorOutlineReader vectors, IArcFlattener flattener, IShapePathWriter writer)
    {
        _vectors = vectors;
        _geometry = new OoxmlGeometry(flattener);
        _writer = writer;
    }

    private readonly DrawingColours _colours = new();

    public OoxmlVectorPaths() : this(new VectorOutlineReader(), new ArcFlattener(), new ShapePathWriter())
    {
    }

    public IReadOnlyList<VectorPath> Paths(VectorGraphicObject vector) =>
        vector.Asset.SvgContent is { Length: > 0 } svg ? [.. _vectors.Read(svg).Recoloured(vector.RecolorHex).Paths] : [];

    public A.CustomGeometry Geometry(ShapeOutline outline) => _geometry.CustomGeometry(outline);

    public ShapeOutline Outline(A.CustomGeometry geometry) => _geometry.OutlineOf(geometry);

    public IEnumerable<OpenXmlElement> Paint(VectorPaint paint, long width, long height)
    {
        yield return paint.FillHex is { } fill
            ? new A.SolidFill(_colours.Of(fill, 1))
            : new A.NoFill();
        yield return paint.StrokeHex is { } stroke
            ? new A.Outline(new A.SolidFill(_colours.Of(stroke, 1)))
            {
                Width = (int)Math.Max(1, paint.StrokeWidth * Math.Min(width, height)),
            }
            : new A.Outline(new A.NoFill());
    }

    public VectorPaint PaintOf(OpenXmlCompositeElement properties)
    {
        var fill = properties.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value;
        var outline = properties.GetFirstChild<A.Outline>();
        var stroke = outline?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value;
        return new VectorPaint
        {
            FillHex = properties.GetFirstChild<A.NoFill>() is not null || fill is null ? null : "#" + fill,
            StrokeHex = stroke is null ? null : "#" + stroke,
            StrokeWidth = 1.0 / Grid * (outline?.Width?.Value ?? 0),
        };
    }

    public string Svg(IReadOnlyList<VectorPath> paths)
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {Grid} {Grid}">""");
        foreach (var path in paths)
        {
            var data = _writer.Write(path.Outline.Scaled(Grid, Grid));
            builder.Append(CultureInfo.InvariantCulture, $"""<path d="{data}" fill="{path.Paint.FillHex ?? "none"}" """);
            if (path.Paint.StrokeHex is { } stroke)
                builder.Append(CultureInfo.InvariantCulture,
                    $"""stroke="{stroke}" stroke-width="{Math.Round(path.Paint.StrokeWidth * Grid, 2).ToString(CultureInfo.InvariantCulture)}" """);
            builder.Append("/>");
        }

        builder.Append("</svg>");
        return builder.ToString();
    }

}

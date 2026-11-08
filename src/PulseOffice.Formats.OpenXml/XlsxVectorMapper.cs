using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using Worksheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxVectorMapper
{

    private const int Grid = 21600;
    private const char PartSeparator = '#';

    private readonly OoxmlVectorPaths _paths;

    public XlsxVectorMapper(
        IVectorOutlineReader vectors, IArcFlattener flattener, IShapePathWriter writer)
    {
        _paths = new OoxmlVectorPaths(vectors, flattener, writer);
    }

    public XlsxVectorMapper()
        : this(new VectorOutlineReader(), new ArcFlattener(), new ShapePathWriter()) { }

    public IReadOnlyList<PlacedObject> ReadVectors(Xdr.WorksheetDrawing drawing, SheetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(layout);

        var anchors = new XlsxAnchorLayout(layout);

        var groups = new Dictionary<string, List<(VectorPath Path, OpenXmlCompositeElement Anchor)>>(
            StringComparer.Ordinal);

        var order = new List<string>();
        var placed = new List<PlacedObject>();

        foreach (var anchor in drawing.ChildElements.OfType<OpenXmlCompositeElement>())
        {
            if (anchor.GetFirstChild<Xdr.Shape>() is not { } element) continue;

            var properties = element.GetFirstChild<Xdr.ShapeProperties>();
            if (properties?.GetFirstChild<A.CustomGeometry>() is not { } geometry) continue;

            var name = element.GetFirstChild<Xdr.NonVisualShapeProperties>()
                ?.GetFirstChild<Xdr.NonVisualDrawingProperties>()?.Name?.Value;

            if (name is null) continue;

            var separator = name.IndexOf(PartSeparator);
            if (separator <= 0) continue;

            var id = name[..separator];

            if (!groups.TryGetValue(id, out var list))
            {
                groups[id] = list = [];
                order.Add(id);
            }

            list.Add((new VectorPath(_paths.Outline(geometry), _paths.PaintOf(properties)), anchor));
        }

        foreach (var id in order)
        {
            var parts = groups[id];
            var anchor = parts[0].Anchor;

            var transform = anchor.GetFirstChild<Xdr.Shape>()
                ?.GetFirstChild<Xdr.ShapeProperties>()?.GetFirstChild<A.Transform2D>();

            placed.Add(anchors.Place(anchor, new PlacedObject
            {
                Id = id,
                Anchor = new CellAddress(0, 0),
                Content = new VectorGraphicObject
                {
                    Id = id,
                    Asset = new VectorAsset
                    {
                        Id = id,
                        Name = id,
                        SourceKind = VectorSourceKind.InlineSvg,
                        SvgContent = _paths.Svg([.. parts.Select(part => part.Path)]),
                    },
                    RotationDegrees = (transform?.Rotation?.Value ?? 0) / 60000.0,
                    FlipHorizontal = transform?.HorizontalFlip?.Value ?? false,
                    FlipVertical = transform?.VerticalFlip?.Value ?? false,
                },
            }, new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(3))));
        }

        return placed;
    }

}

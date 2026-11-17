using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Drawing;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioDrawCommands(IActiveFolioContext context, IFolioDrawingTools tools) : FolioCommandSet(context)
{
    internal static readonly IReadOnlyList<(string Id, DrawingTool Tool, bool Filled)> Pens =
    [
        ("folio.draw.freeform", DrawingTool.Freehand, false),
        ("folio.draw.freeformFilled", DrawingTool.Freehand, true),
        ("folio.draw.curve", DrawingTool.Curve, false),
        ("folio.draw.curveFilled", DrawingTool.Curve, true),
        ("folio.draw.polygon", DrawingTool.Polygon, false),
        ("folio.draw.polygonFilled", DrawingTool.Polygon, true),
        ("folio.draw.polygon45", DrawingTool.PolygonConstrained, false),
        ("folio.draw.polygon45Filled", DrawingTool.PolygonConstrained, true),
        ("folio.insert.frameInteractive", DrawingTool.Frame, false),
    ];

    internal static readonly IReadOnlyList<(string Id, ConnectorKind Kind)> Connectors =
    [
        ("folio.draw.connector", ConnectorKind.Straight),
        ("folio.draw.connectorElbow", ConnectorKind.Elbow),
        ("folio.draw.connectorCurved", ConnectorKind.Curved),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. Pens.Select(pen => Action(Editing(pen.Id) with
        {
            Surface = pen.Id.StartsWith("folio.draw.", StringComparison.Ordinal) ? CommandSurface.Palette : CommandSurface.Advanced,
            CheckedWhen = _ => tools.Tool == pen.Tool && tools.Filled == pen.Filled,
        }, _ => tools.Choose(pen.Tool, pen.Filled))),
        .. Connectors.Select(connector => Action(Editing(connector.Id) with
        {
            Surface = CommandSurface.Palette,
            CheckedWhen = _ => tools.Tool == DrawingTool.Connector && tools.Connector == connector.Kind,
        }, _ => tools.ChooseConnector(connector.Kind))),
    ];
}

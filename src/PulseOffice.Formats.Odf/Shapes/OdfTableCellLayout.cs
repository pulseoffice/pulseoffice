using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfTableCellLayout
{
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Fo = OdfPackage.Namespaces.Fo;

    private const double MostMarginPoints = 1584;

    private readonly OdfMeasure _measure = new();

    public void Write(XElement cellProperties, TableCellFormat? format)
    {
        if (format is null) return;
        if (format.Margins is { } margins)
            cellProperties.Add(
                new XAttribute(Fo + "padding-left", Points(margins.Left)),
                new XAttribute(Fo + "padding-right", Points(margins.Right)),
                new XAttribute(Fo + "padding-top", Points(margins.Top)),
                new XAttribute(Fo + "padding-bottom", Points(margins.Bottom)));
        switch (format.Direction)
        {
            case CellTextDirection.Down:
                cellProperties.Add(new XAttribute(Style + "rotation-angle", "270"));
                break;
            case CellTextDirection.Up:
                cellProperties.Add(new XAttribute(Style + "rotation-angle", "90"));
                break;
            case CellTextDirection.Stacked:
                cellProperties.Add(new XAttribute(Style + "direction", "ttb"));
                break;
        }
    }

    public TableCellFormat Read(TableCellFormat format, XElement? style)
    {
        var cell = style?.Element(Style + "table-cell-properties");
        if (cell is null) return format;
        var padding = (string?)cell.Attribute(Fo + "padding");
        double? Side(string side) => ((string?)cell.Attribute(Fo + $"padding-{side}") ?? padding) is { } text
            && _measure.ParseLength(text, new Twip(-1)) is { Value: >= 0 } length ? Math.Min(length.ToPoints(), MostMarginPoints) : null;
        var left = Side("left");
        var right = Side("right");
        var top = Side("top");
        var bottom = Side("bottom");
        var margins = left is null && right is null && top is null && bottom is null ? null : new TableCellMargins(left ?? 0, right ?? 0, top ?? 0, bottom ?? 0);
        var direction = (string?)cell.Attribute(Style + "direction") == "ttb"
            ? CellTextDirection.Stacked
            : _measure.ParseNumber((string?)cell.Attribute(Style + "rotation-angle"), 0) switch
            {
                270 => CellTextDirection.Down,
                90 => CellTextDirection.Up,
                _ => CellTextDirection.Horizontal,
            };
        return format with { Margins = margins ?? format.Margins, Direction = direction };
    }

    private string Points(double value) => $"{_measure.Number(Math.Clamp(value, 0, MostMarginPoints))}pt";
}

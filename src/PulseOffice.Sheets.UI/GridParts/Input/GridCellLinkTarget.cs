using Avalonia;
using Avalonia.Media;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.UI.GridParts.Input;

internal sealed class GridCellLinkTarget(GridViewport viewport, ICellPresenter presenter, GridRenderServices services, GridDisplayOptions options, ICellLinkHitArea area)
{
    public string? LinkAt(Point point, bool whateverTheSetting = false)
    {
        if ((!whateverTheSetting && !options.CellLinks.FollowsOnClick) || viewport.Sheet is not { } sheet || viewport.CellAt(point) is not { } address) return null;
        var cell = sheet[address];
        if (cell.Hyperlink is not { Length: > 0 } link || cell.Value.IsEmpty) return null;

        var bounds = viewport.MergedRect(address);
        if (!bounds.Contains(point)) return null;
        var styles = viewport.Engine.Workbook.Styles;
        var style = styles.Get(sheet.StyleIdAt(address));
        var presented = presenter.Present(cell with { StyleId = sheet.StyleIdAt(address) }, styles, viewport.Engine.Workbook.NumberFormats, bounds.Width / (viewport.Metrics.FontSize * 0.6));
        var text = services.TextComposer.Compose(presented.Text, style.Font, Brushes.Transparent);
        var span = area.Span(bounds.X, bounds.Right, text.Width, 3.0 + style.Indent * 8, style.Horizontal, presented.AlignRight,
            style.WrapText || style.RotationDegrees % 360 != 0);
        return span.Contains(point.X) ? link : null;
    }
}

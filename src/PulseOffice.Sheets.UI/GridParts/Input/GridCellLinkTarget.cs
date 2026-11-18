using Avalonia;
using Avalonia.Media;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.UI.GridParts.Input;

internal sealed class GridCellLinkTarget(
    GridViewport viewport,
    ICellPresenter presenter,
    GridRenderServices services,
    GridDisplayOptions options,
    ICellLinkHitArea area,
    ConditionalFormattingEvaluator conditional,
    ConditionalIcons icons)
{
    private const int OverflowReach = 32;

    public string? LinkAt(Point point, bool whateverTheSetting = false)
    {
        if ((!whateverTheSetting && !options.CellLinks.FollowsOnClick) || viewport.Sheet is not { } sheet || viewport.CellAt(point) is not { } address) return null;
        var rules = conditional.Begin(sheet);
        if (!sheet[viewport.AnchorOf(address)].Value.IsEmpty) return LinkOf(sheet, rules, viewport.AnchorOf(address), point, own: true);
        if (sheet.Merges.At(address) is not null) return null;

        for (var step = 1; step <= OverflowReach; step++)
        {
            foreach (var column in new[] { address.Column - step, address.Column + step })
            {
                if (column < 0 || column >= CellAddress.MaxColumns) continue;
                var neighbour = new CellAddress(address.Row, column);
                if (sheet.Merges.At(neighbour) is not null || sheet[neighbour].Value.IsEmpty) continue;
                if (LinkOf(sheet, rules, neighbour, point, own: false) is { } link) return link;
            }
        }

        return null;
    }

    private string? LinkOf(Worksheet sheet, ConditionalFormattingPass rules, CellAddress address, Point point, bool own)
    {
        var cell = sheet[address];
        if (cell.Hyperlink is not { Length: > 0 } link || cell.Value.IsEmpty) return null;
        if (!own && cell.Value.Kind != ValueKind.Text) return null;

        var bounds = viewport.MergedRect(address);
        var styles = viewport.Engine.Workbook.Styles;
        var style = styles.Get(sheet.StyleIdAt(address));
        if (!rules.IsEmpty)
        {
            var appearance = rules.At(address, viewport.Engine.ContextAt(new SheetCell(viewport.SheetIndex, address)));
            if (appearance.HideValue) return null;
            style = appearance.Over(style);
            if (appearance.IconIndex is not null) bounds = bounds.Deflate(new Thickness(icons.Reserve(bounds), 0, 0, 0));
        }

        if (own && !bounds.Contains(point)) return null;
        var presented = presenter.Present(cell with { StyleId = sheet.StyleIdAt(address) }, styles, viewport.Engine.Workbook.NumberFormats, bounds.Width / (viewport.Metrics.FontSize * 0.6));
        var text = services.TextComposer.Compose(presented.Text, style.Font, Brushes.Transparent);
        var padding = 3.0 + style.Indent * 8;
        var wraps = style.WrapText || style.RotationDegrees % 360 != 0;
        var overflows = !wraps && sheet.Merges.At(address) is null && cell.Value.Kind == ValueKind.Text && text.Width > bounds.Width - padding * 2;

        var span = overflows
            ? area.Spilled(bounds.X, bounds.Right, services.Overflow.Span(sheet, address, style, bounds.X, bounds.Right, viewport.Geometry.ColumnWidth), text.Width, padding, style.Horizontal)
            : own ? area.Span(bounds.X, bounds.Right, text.Width, padding, style.Horizontal, presented.AlignRight, wraps) : null;
        return span is not null && span.Contains(point.X) && point.Y >= bounds.Y && point.Y <= bounds.Bottom ? link : null;
    }
}

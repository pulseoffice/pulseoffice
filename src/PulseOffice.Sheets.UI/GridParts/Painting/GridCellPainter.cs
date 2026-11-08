using PulseOffice.Sheets.Engine.Operations;
using Avalonia;
using Avalonia.Media;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.GridParts.Painting;

internal sealed class GridCellPainter(
    GridViewport viewport,
    ICellPresenter presenter,
    ConditionalFormattingEvaluator conditional,
    ConditionalIcons icons,
    GridDisplayOptions options,
    GridRenderServices services,
    IGridTheme theme,
    CellDecorationPainter decorations,
    CellValuePainter values)
{
    private const int OverflowReach = 32;

    public void PaintFrozenPanes(DrawingContext context, VisibleRange visible, IBrush foreground, IBrush lineBrush)
    {
        var geometry = viewport.Geometry;
        if (!geometry.HasFrozenPanes) return;

        var area = viewport.GridArea();
        var separator = new Pen(theme.Brush(Palette.Keys.BorderStrong, Brushes.Gray), 1);
        var scroll = viewport.Scroll;
        var paneScroll = viewport.PaneScroll;

        if (geometry.FrozenRows > 0)
        {
            var strip = new Rect(area.X + geometry.FrozenWidth, area.Y,
                Math.Max(area.Width - geometry.FrozenWidth, 0), geometry.FrozenHeight);

            var band = viewport.BandRows();

            using (context.PushClip(strip))
                Paint(context,
                    new VisibleRange(band.First, band.Last, visible.FirstColumn, visible.LastColumn),
                    foreground, lineBrush, new Vector(scroll.X, paneScroll.Y));

            context.DrawLine(separator,
                new Point(area.X, area.Y + geometry.FrozenHeight),
                new Point(area.Right, area.Y + geometry.FrozenHeight));
        }

        if (geometry.FrozenColumns > 0)
        {
            var strip = new Rect(area.X, area.Y + geometry.FrozenHeight,
                geometry.FrozenWidth, Math.Max(area.Height - geometry.FrozenHeight, 0));

            var band = viewport.BandColumns();

            using (context.PushClip(strip))
                Paint(context,
                    new VisibleRange(visible.FirstRow, visible.LastRow, band.First, band.Last),
                    foreground, lineBrush, new Vector(paneScroll.X, scroll.Y));

            context.DrawLine(separator,
                new Point(area.X + geometry.FrozenWidth, area.Y),
                new Point(area.X + geometry.FrozenWidth, area.Bottom));
        }

        if (geometry.FrozenRows > 0 && geometry.FrozenColumns > 0)
        {
            var corner = new Rect(area.X, area.Y, geometry.FrozenWidth, geometry.FrozenHeight);

            var rows = viewport.BandRows();
            var columns = viewport.BandColumns();

            using (context.PushClip(corner))
                Paint(context,
                    new VisibleRange(rows.First, rows.Last, columns.First, columns.Last),
                    foreground, lineBrush, paneScroll);
        }
    }

    public void Paint(
        DrawingContext context, VisibleRange visible, IBrush foreground, IBrush lineBrush,
        Vector? origin = null)
    {
        var offset = origin ?? viewport.Scroll;

        var sheet = viewport.Sheet;
        if (sheet is null) return;

        var geometry = viewport.Geometry;
        var engine = viewport.Engine;
        var pen = new Pen(lineBrush);

        for (var row = visible.FirstRow; row <= visible.LastRow; row++)
        {
            var height = geometry.RowHeight(row);
            if (height <= 0) continue;

            var y = geometry.ColumnHeaderHeight + geometry.RowOffset(row) - offset.Y;

            for (var column = visible.FirstColumn; column <= visible.LastColumn; column++)
            {
                var width = geometry.ColumnWidth(column);
                if (width <= 0) continue;

                var x = geometry.RowHeaderWidth + geometry.ColumnOffset(column) - offset.X;
                var visibleCell = new CellAddress(row, column);

                var merge = sheet.Merges.At(visibleCell);
                if (merge is { } covering && DrawingAnchor(covering, visible) != visibleCell) continue;
                var address = merge?.Start ?? visibleCell;

                var bounds = merge is { } region
                    ? viewport.MergedRect(region, offset)
                    : new Rect(x, y, width, height);

                if (options.ShowGridLines)
                {
                    context.DrawLine(pen,
                        new Point(bounds.Right, bounds.Y), new Point(bounds.Right, bounds.Bottom));
                    context.DrawLine(pen,
                        new Point(bounds.X, bounds.Bottom), new Point(bounds.Right, bounds.Bottom));
                }

                var cell = sheet[address] with { StyleId = sheet.StyleIdAt(address) };
                var style = engine.Workbook.Styles.Get(cell.StyleId);

                var appearance = sheet.ConditionalRules.Count == 0
                    ? ConditionalAppearance.None
                    : conditional.Evaluate(sheet, address, engine.ContextAt(new SheetCell(viewport.SheetIndex, address)));
                style = appearance.Over(style);

                decorations.DrawStyleBackground(context, style, bounds);
                decorations.DrawConditionalFill(context, appearance, bounds);
                icons.Draw(context, appearance, bounds);
                decorations.DrawStyleBorders(context, style, bounds);

                if (engine.Workbook.Changes.ShowChanges
                    && engine.Workbook.Changes.At(viewport.SheetIndex, address) is not null)
                    decorations.DrawChangeMark(context, bounds);

                if (sheet.Sparklines.At(address) is { } spark)
                    decorations.DrawSparkline(context, bounds, sheet, spark.Group, spark.Sparkline);

                if (decorations.FilterButtonAt(sheet, address) is { } filterColumn)
                    decorations.DrawFilterButton(context, bounds, sheet.AutoFilter.IsFiltered(filterColumn));

                if (sheet.Comments.ContainsKey(address)) decorations.DrawCommentMark(context, bounds);
            }
        }

        PaintTexts(context, sheet, visible, foreground, offset);
    }

    private CellAddress DrawingAnchor(CellRange region, VisibleRange visible) => new(
        Math.Max(region.Start.Row, visible.FirstRow),
        Math.Max(region.Start.Column, visible.FirstColumn));

    private void PaintTexts(DrawingContext context, Worksheet sheet, VisibleRange visible, IBrush foreground, Vector offset)
    {
        var geometry = viewport.Geometry;
        var engine = viewport.Engine;
        var firstColumn = Math.Max(visible.FirstColumn - OverflowReach, 0);
        var lastColumn = Math.Min(visible.LastColumn + OverflowReach, CellAddress.MaxColumns - 1);

        for (var row = visible.FirstRow; row <= visible.LastRow; row++)
        {
            var height = geometry.RowHeight(row);
            if (height <= 0) continue;

            var y = geometry.ColumnHeaderHeight + geometry.RowOffset(row) - offset.Y;

            for (var column = firstColumn; column <= lastColumn; column++)
            {
                var width = geometry.ColumnWidth(column);
                if (width <= 0) continue;

                var visibleCell = new CellAddress(row, column);
                var merge = sheet.Merges.At(visibleCell);
                if (merge is { } covering && DrawingAnchor(covering, visible) != visibleCell) continue;
                var address = merge?.Start ?? visibleCell;
                var cell = sheet[address];
                if (cell.Value.IsEmpty) continue;

                var inside = column >= visible.FirstColumn && column <= visible.LastColumn;
                if (!inside && (merge is not null || cell.Value.Kind != ValueKind.Text)) continue;

                var x = geometry.RowHeaderWidth + geometry.ColumnOffset(column) - offset.X;
                var bounds = merge is { } region
                    ? viewport.MergedRect(region, offset)
                    : new Rect(x, y, width, height);

                cell = cell with { StyleId = sheet.StyleIdAt(address) };
                var style = engine.Workbook.Styles.Get(cell.StyleId);
                if (sheet.ConditionalRules.Count > 0)
                {
                    var appearance = conditional.Evaluate(sheet, address, engine.ContextAt(new SheetCell(viewport.SheetIndex, address)));
                    if (appearance.HideValue) continue;
                    style = appearance.Over(style);
                    if (appearance.IconIndex is not null) bounds = bounds.Deflate(new Thickness(icons.Reserve(bounds), 0, 0, 0));
                }

                var presented = options.ShowFormulas && cell.HasFormula
                    ? new FormattedCell(cell.Formula!, null, false)
                    : presenter.Present(cell, engine.Workbook.Styles, engine.Workbook.NumberFormats, bounds.Width / (viewport.Metrics.FontSize * 0.6));

                if (options.HighlightValues && !options.ShowFormulas)
                    presented = presented with
                    {
                        ColorName = cell.HasFormula
                            ? "BLUE"
                            : cell.Value.Kind == ValueKind.Number ? "GREEN" : null,
                    };

                var overflow = merge is null
                    ? OverflowOf(sheet, address, style, bounds)
                    : bounds;

                values.Paint(context, presented, style, foreground, bounds, overflow, address);
            }
        }
    }

    private Rect OverflowOf(Worksheet sheet, CellAddress address, CellStyle style, Rect bounds)
    {
        var span = services.Overflow.Span(sheet, address, style, bounds.X, bounds.Right, viewport.Geometry.ColumnWidth);
        return new Rect(span.Left, bounds.Y, Math.Max(span.Right - span.Left, bounds.Width), bounds.Height);
    }
}

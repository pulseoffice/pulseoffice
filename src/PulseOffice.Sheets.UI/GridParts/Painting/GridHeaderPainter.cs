using Avalonia;
using Avalonia.Media;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.GridParts.Painting;

internal sealed class GridHeaderPainter(GridViewport viewport, SheetSelection selection, OutlineGutterPainter gutter)
{
    public void Paint(
        DrawingContext context, VisibleRange visible,
        IBrush headerBrush, IBrush foreground, IBrush lineBrush, IBrush accent)
    {
        var geometry = viewport.Geometry;
        var surface = viewport.Surface;
        var pen = new Pen(lineBrush);

        var accentColor = (accent as SolidColorBrush)?.Color ?? Colors.SteelBlue;
        var selectedFill = new SolidColorBrush(accentColor, 0.16);
        var selectedText = new SolidColorBrush(accentColor);
        var range = selection.Range;

        context.FillRectangle(headerBrush, new Rect(0, 0, surface.Width, geometry.ColumnHeaderHeight));

        void ColumnHeader(int column, double offset)
        {
            var x = geometry.RowHeaderWidth + geometry.ColumnOffset(column) - offset;
            var width = geometry.ColumnWidth(column);
            if (width <= 0) return;

            var columnSelected = column >= range.Start.Column && column <= range.End.Column;
            var columnArea = new Rect(x, geometry.ColumnGutterHeight, width, geometry.ColumnHeaderHeight - geometry.ColumnGutterHeight);

            if (columnSelected) context.FillRectangle(selectedFill, columnArea);

            DrawCentred(context, CellAddress.ColumnName(column),
                columnSelected ? selectedText : foreground, columnArea);

            context.DrawLine(pen,
                new Point(x + width, geometry.ColumnGutterHeight), new Point(x + width, geometry.ColumnHeaderHeight));
        }

        void RowHeader(int row, double offset)
        {
            var y = geometry.ColumnHeaderHeight + geometry.RowOffset(row) - offset;
            var height = geometry.RowHeight(row);
            if (height <= 0) return;

            var rowSelected = row >= range.Start.Row && row <= range.End.Row;
            var rowArea = new Rect(geometry.RowGutterWidth, y, geometry.RowHeaderWidth - geometry.RowGutterWidth, height);

            if (rowSelected) context.FillRectangle(selectedFill, rowArea);

            DrawCentred(context, (row + 1).ToString(System.Globalization.CultureInfo.CurrentCulture),
                rowSelected ? selectedText : foreground, rowArea);

            context.DrawLine(pen,
                new Point(geometry.RowGutterWidth, y + height), new Point(geometry.RowHeaderWidth, y + height));
        }

        var columnsArea = new Rect(
            geometry.RowHeaderWidth + geometry.FrozenWidth, 0,
            Math.Max(surface.Width - geometry.RowHeaderWidth - geometry.FrozenWidth, 0),
            geometry.ColumnHeaderHeight);

        using (context.PushClip(columnsArea))
            for (var column = visible.FirstColumn; column <= visible.LastColumn; column++)
                ColumnHeader(column, viewport.Scroll.X);

        if (geometry.FrozenColumns > 0)
        {
            var band = viewport.BandColumns();
            var bandArea = new Rect(
                geometry.RowHeaderWidth, 0, geometry.FrozenWidth, geometry.ColumnHeaderHeight);

            using (context.PushClip(bandArea))
                for (var column = band.First; column <= band.Last; column++)
                    ColumnHeader(column, viewport.PaneScroll.X);
        }

        context.FillRectangle(headerBrush, new Rect(0, 0, geometry.RowHeaderWidth, surface.Height));

        var rowsArea = new Rect(
            0, geometry.ColumnHeaderHeight + geometry.FrozenHeight, geometry.RowHeaderWidth,
            Math.Max(surface.Height - geometry.ColumnHeaderHeight - geometry.FrozenHeight, 0));

        using (context.PushClip(rowsArea))
            for (var row = visible.FirstRow; row <= visible.LastRow; row++)
                RowHeader(row, viewport.Scroll.Y);

        if (geometry.FrozenRows > 0)
        {
            var band = viewport.BandRows();
            var bandArea = new Rect(
                0, geometry.ColumnHeaderHeight, geometry.RowHeaderWidth, geometry.FrozenHeight);

            using (context.PushClip(bandArea))
                for (var row = band.First; row <= band.Last; row++)
                    RowHeader(row, viewport.PaneScroll.Y);
        }

        context.FillRectangle(headerBrush,
            new Rect(0, 0, geometry.RowHeaderWidth, geometry.ColumnHeaderHeight));

        gutter.Paint(context, headerBrush, foreground, lineBrush);
    }

    private void DrawCentred(DrawingContext context, string text, IBrush brush, Rect bounds)
    {
        var formatted = new FormattedText(
            text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, 11, brush);

        using (context.PushClip(bounds))
            context.DrawText(formatted, new Point(
                bounds.X + (bounds.Width - formatted.Width) / 2,
                bounds.Y + (bounds.Height - formatted.Height) / 2));
    }
}

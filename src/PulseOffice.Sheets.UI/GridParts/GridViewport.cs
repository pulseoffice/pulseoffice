using PulseOffice.Foundation.Units;
using Avalonia;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.GridParts;

internal sealed class GridViewport
{
    private const double DefaultRowHeaderWidth = 44;
    private const double DefaultColumnHeaderHeight = 20;
    private const double RowHeaderDigitWidth = 7;
    private const double RowHeaderPadding = 14;

    private readonly CalculationEngine _engine;
    private readonly Func<Size> _bounds;
    private readonly Dictionary<Worksheet, GridGeometry> _geometries = [];
    private readonly GridGeometry _detached;

    public GridViewport(CalculationEngine engine, DesignTokens.Metrics metrics, int sheetIndex, Func<Size> bounds, Outline.IOutlineGutter gutter)
    {
        _engine = engine;
        Gutter = gutter;
        _bounds = bounds;
        Metrics = metrics;
        SheetIndex = sheetIndex;
        _detached = new GridGeometry { DefaultRowHeight = metrics.RowHeight };
    }

    public CalculationEngine Engine => _engine;

    public Outline.IOutlineGutter Gutter { get; }

    public DesignTokens.Metrics Metrics { get; }

    public int SheetIndex { get; set; }

    public bool ShowHeaders { get; set; } = true;

    public double Zoom { get; set; } = 1;

    public Vector Scroll { get; set; }

    public Vector PaneScroll { get; set; }

    public Worksheet? Sheet => _engine.SheetAtIndex(SheetIndex);

    public Size Surface
    {
        get
        {
            var bounds = _bounds();
            return new Size(bounds.Width / Zoom, bounds.Height / Zoom);
        }
    }

    public GridGeometry Geometry => GeometryFor(Sheet);

    private GridGeometry GeometryFor(Worksheet? sheet)
    {
        if (sheet is null) return WithHeaders(_detached, null);

        if (_geometries.TryGetValue(sheet, out var geometry)) return WithHeaders(geometry, sheet);

        foreach (var stale in _geometries.Keys.Where(known => !ReferenceEquals(known.Owner, _engine.Workbook)).ToList())
            _geometries.Remove(stale);

        geometry = new GridGeometry(sheet.Layout);
        _geometries[sheet] = geometry;

        return WithHeaders(geometry, sheet);
    }

    private GridGeometry WithHeaders(GridGeometry geometry, Worksheet? sheet)
    {
        geometry.RowGutterWidth = ShowHeaders && sheet is not null ? Gutter.Thickness(sheet, PulseOffice.Sheets.Engine.Operations.StructureAxis.Rows) : 0;
        geometry.ColumnGutterHeight = ShowHeaders && sheet is not null ? Gutter.Thickness(sheet, PulseOffice.Sheets.Engine.Operations.StructureAxis.Columns) : 0;
        geometry.RowHeaderWidth = ShowHeaders ? RowHeaderWidthFor(geometry) + geometry.RowGutterWidth : 0;
        geometry.ColumnHeaderHeight = ShowHeaders ? DefaultColumnHeaderHeight + geometry.ColumnGutterHeight : 0;

        return geometry;
    }

    private double RowHeaderWidthFor(GridGeometry geometry)
    {
        var lastVisibleRow = geometry.RowAt(Math.Max(Scroll.Y, 0) + geometry.FrozenHeight + Surface.Height);
        var digits = (Math.Max(lastVisibleRow, 0) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture).Length;

        return Math.Max(DefaultRowHeaderWidth, digits * RowHeaderDigitWidth + RowHeaderPadding);
    }

    public Point ToLocal(Point point) => new(point.X / Zoom, point.Y / Zoom);

    public Rect ToScreen(Rect bounds) => new(bounds.X * Zoom, bounds.Y * Zoom, bounds.Width * Zoom, bounds.Height * Zoom);

    public void ApplyFrozenPanes()
    {
        var frozen = Sheet?.Frozen ?? FrozenPanes.None;
        if (Geometry.FrozenRows == frozen.Rows && Geometry.FrozenColumns == frozen.Columns) return;
        Geometry.FrozenRows = frozen.Rows;
        Geometry.FrozenColumns = frozen.Columns;
        Scroll = default;
    }

    public void FreezePanes(int rows, int columns)
    {
        if (Sheet is { } sheet) sheet.Frozen = new FrozenPanes(Math.Max(rows, 0), Math.Max(columns, 0));
        Geometry.FrozenRows = Math.Max(rows, 0);
        Geometry.FrozenColumns = Math.Max(columns, 0);

        Scroll = default;
    }

    public void SplitPanes(int rows, int columns)
    {
        Geometry.FrozenRows = rows;
        Geometry.FrozenColumns = columns;
        Geometry.PanesAreSplit = rows > 0 || columns > 0;

        PaneScroll = default;
    }

    public void ApplyRowFilter(Worksheet sheet, IReadOnlySet<int>? hiddenRows)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (!ReferenceEquals(sheet.Owner, _engine.Workbook)) return;

        sheet.Layout.ApplyFilteredRows(hiddenRows);
    }

    public Rect GridArea() => new(
        Geometry.RowHeaderWidth, Geometry.ColumnHeaderHeight,
        Math.Max(Surface.Width - Geometry.RowHeaderWidth, 0),
        Math.Max(Surface.Height - Geometry.ColumnHeaderHeight, 0));

    public Rect ScrollableArea()
    {
        var area = GridArea();
        return new Rect(
            area.X + Geometry.FrozenWidth,
            area.Y + Geometry.FrozenHeight,
            Math.Max(area.Width - Geometry.FrozenWidth, 0),
            Math.Max(area.Height - Geometry.FrozenHeight, 0));
    }

    public Rect CellRect(CellAddress address)
    {
        var (x, y, width, height) = Geometry.CellBounds(address);

        var offsetX = address.Column < Geometry.FrozenColumns ? PaneScroll.X : Scroll.X;
        var offsetY = address.Row < Geometry.FrozenRows ? PaneScroll.Y : Scroll.Y;

        return new Rect(
            Geometry.RowHeaderWidth + x - offsetX,
            Geometry.ColumnHeaderHeight + y - offsetY,
            width, height);
    }

    public Rect MergedRect(CellRange region, Vector offset)
    {
        var x = Geometry.RowHeaderWidth + Geometry.ColumnOffset(region.Start.Column) - offset.X;
        var y = Geometry.ColumnHeaderHeight + Geometry.RowOffset(region.Start.Row) - offset.Y;

        var width = Geometry.ColumnOffset(region.End.Column) + Geometry.ColumnWidth(region.End.Column) - Geometry.ColumnOffset(region.Start.Column);
        var height = Geometry.RowOffset(region.End.Row) + Geometry.RowHeight(region.End.Row) - Geometry.RowOffset(region.Start.Row);

        return new Rect(x, y, width, height);
    }

    public Rect MergedRect(CellAddress address)
    {
        if (Sheet?.Merges.At(address) is not { } region) return CellRect(address);

        var start = CellRect(region.Start);
        var end = CellRect(region.End);

        return new Rect(start.X, start.Y, Math.Max(end.Right - start.X, 0), Math.Max(end.Bottom - start.Y, 0));
    }

    public Rect SpanRect(CellRange range)
    {
        var start = CellRect(range.Start);
        var end = CellRect(range.End);
        return new Rect(start.X, start.Y, end.Right - start.X, end.Bottom - start.Y);
    }

    public Rect DocumentBounds(CellAddress address)
    {
        var region = Sheet?.Merges.At(address) ?? new CellRange(address, address);
        var (x, y, _, _) = Geometry.CellBounds(region.Start);
        var (endX, endY, endWidth, endHeight) = Geometry.CellBounds(region.End);

        return new Rect(x, y, endX + endWidth - x, endY + endHeight - y);
    }

    public (int First, int Last) BandRows()
    {
        var first = Math.Max(Geometry.RowAt(PaneScroll.Y), 0);
        var last = first;
        var used = Geometry.RowOffset(first) - PaneScroll.Y;

        while (last < CellAddress.MaxRows - 1 && used < Geometry.FrozenHeight)
        {
            used += Geometry.RowHeight(last);
            last++;
        }

        return (first, last);
    }

    public (int First, int Last) BandColumns()
    {
        var first = Math.Max(Geometry.ColumnAt(PaneScroll.X), 0);
        var last = first;
        var used = Geometry.ColumnOffset(first) - PaneScroll.X;

        while (last < CellAddress.MaxColumns - 1 && used < Geometry.FrozenWidth)
        {
            used += Geometry.ColumnWidth(last);
            last++;
        }

        return (first, last);
    }

    public bool InFrozenColumns(Point point) =>
        Geometry.FrozenColumns > 0 && point.X - Geometry.RowHeaderWidth < Geometry.FrozenWidth;

    public bool InFrozenRows(Point point) =>
        Geometry.FrozenRows > 0 && point.Y - Geometry.ColumnHeaderHeight < Geometry.FrozenHeight;

    private double OffsetXAt(Point point) => InFrozenColumns(point) ? PaneScroll.X : Scroll.X;

    private double OffsetYAt(Point point) => InFrozenRows(point) ? PaneScroll.Y : Scroll.Y;

    public int ColumnBorderAt(Point point) =>
        point.Y < Geometry.ColumnHeaderHeight && point.X >= Geometry.RowHeaderWidth
            ? Geometry.ColumnBorderAt(point.X - Geometry.RowHeaderWidth + OffsetXAt(point))
            : -1;

    public int RowBorderAt(Point point) =>
        point.X < Geometry.RowHeaderWidth && point.Y >= Geometry.ColumnHeaderHeight
            ? Geometry.RowBorderAt(point.Y - Geometry.ColumnHeaderHeight + OffsetYAt(point))
            : -1;

    public Outline.OutlineButton? OutlineButtonAt(Point point)
    {
        if (Sheet is not { } sheet) return null;
        var geometry = Geometry;
        var rows = PulseOffice.Sheets.Engine.Operations.StructureAxis.Rows;
        var columns = PulseOffice.Sheets.Engine.Operations.StructureAxis.Columns;

        if (point.X < geometry.RowGutterWidth)
        {
            if (point.Y >= geometry.ColumnHeaderHeight)
                return Gutter.ToggleAt(sheet, rows, point.X, geometry.RowAt(point.Y - geometry.ColumnHeaderHeight + OffsetYAt(point)));
            return point.Y >= geometry.ColumnGutterHeight ? Gutter.LevelAt(sheet, rows, point.X) : null;
        }

        if (point.Y < geometry.ColumnGutterHeight)
        {
            if (point.X >= geometry.RowHeaderWidth)
                return Gutter.ToggleAt(sheet, columns, point.Y, geometry.ColumnAt(point.X - geometry.RowHeaderWidth + OffsetXAt(point)));
            return point.X >= geometry.RowGutterWidth ? Gutter.LevelAt(sheet, columns, point.Y) : null;
        }

        return null;
    }

    public SheetGridView.GridHit HitTest(Point point)
    {
        var overRowHeader = point.X < Geometry.RowHeaderWidth;
        var overColumnHeader = point.Y < Geometry.ColumnHeaderHeight;

        if (overRowHeader && overColumnHeader) return new SheetGridView.GridHit(SheetGridView.GridPart.Corner, -1, -1);

        if (overRowHeader)
            return new SheetGridView.GridHit(
                SheetGridView.GridPart.RowHeader,
                Geometry.RowAt(point.Y - Geometry.ColumnHeaderHeight + OffsetYAt(point)), -1);

        if (overColumnHeader)
            return new SheetGridView.GridHit(
                SheetGridView.GridPart.ColumnHeader, -1,
                Geometry.ColumnAt(point.X - Geometry.RowHeaderWidth + OffsetXAt(point)));

        var row = Geometry.RowAt(point.Y - Geometry.ColumnHeaderHeight + OffsetYAt(point));
        var column = Geometry.ColumnAt(point.X - Geometry.RowHeaderWidth + OffsetXAt(point));

        return row >= 0 && column >= 0
            ? new SheetGridView.GridHit(SheetGridView.GridPart.Cell, row, column)
            : new SheetGridView.GridHit(SheetGridView.GridPart.Corner, -1, -1);
    }

    public CellAddress? CellAt(Point point) =>
        HitTest(point).Address is { } address ? AnchorOf(address) : null;

    public CellAddress AnchorOf(CellAddress address) =>
        Sheet?.Merges.At(address) is { } region ? region.Start : address;

    public CellAddress AnchorAt(double x, double y)
    {
        var column = Geometry.ColumnAt(Math.Max(0, x - Geometry.RowHeaderWidth + Scroll.X));
        var row = Geometry.RowAt(Math.Max(0, y - Geometry.ColumnHeaderHeight + Scroll.Y));

        return new CellAddress(Math.Max(0, row), Math.Max(0, column));
    }

    public SnapLines CellEdges()
    {
        var area = GridArea();
        return new SnapLines(
            Edges(Math.Max(0, Geometry.ColumnAt(Math.Max(0, Scroll.X))), CellAddress.MaxColumns, column => CellRect(new CellAddress(0, column)).X, area.Right),
            Edges(Math.Max(0, Geometry.RowAt(Math.Max(0, Scroll.Y))), CellAddress.MaxRows, row => CellRect(new CellAddress(row, 0)).Y, area.Bottom));
    }

    private List<double> Edges(int first, int count, Func<int, double> edgeAt, double limit)
    {
        var edges = new List<double>();
        var end = edgeAt(count - 1);
        for (var index = first; index < count; index++)
        {
            var edge = edgeAt(index);
            if (edge > limit || edge >= end && edges.Count > 0 && edges[^1] >= end) break;
            if (edges.Count == 0 || edges[^1] != edge) edges.Add(edge);
        }
        return edges;
    }

    public Rect BoundsOf(PlacedObject placed)
    {
        var anchor = CellRect(placed.Anchor);

        return new Rect(
            anchor.X + GridGeometry.ToPixels(placed.OffsetX),
            anchor.Y + GridGeometry.ToPixels(placed.OffsetY),
            GridGeometry.ToPixels(placed.Size.Width),
            GridGeometry.ToPixels(placed.Size.Height));
    }

    public double RotationOf(PlacedObject placed) => placed.Content switch
    {
        ShapeObject shape => shape.RotationDegrees,
        ShapeGroupObject group => group.RotationDegrees,
        PulseOffice.Drawing.Assets.VectorGraphicObject vector => vector.RotationDegrees,
        _ => 0,
    };

    public SheetGridView.ObjectChange ChangeOf(PlacedObject placed, ObjectBox box, double? rotation)
    {
        var address = AnchorAt(box.X, box.Y);
        var anchor = CellRect(address);

        return new SheetGridView.ObjectChange(
            placed,
            address,
            GridGeometry.ToTwips(box.X - anchor.X),
            GridGeometry.ToTwips(box.Y - anchor.Y),
            new DocumentSize(GridGeometry.ToTwips(box.Width), GridGeometry.ToTwips(box.Height)),
            rotation);
    }

    public int PageRows()
    {
        var area = GridArea();
        var visible = Geometry.VisibleCells(Scroll.X, Scroll.Y, area.Width - Geometry.FrozenWidth, area.Height - Geometry.FrozenHeight);
        return Math.Max(visible.RowCount - 1, 1);
    }

    public void EnsureVisible(CellAddress address)
    {
        var area = GridArea();
        var bounds = DocumentBounds(address);

        if (address.Column >= Geometry.FrozenColumns)
        {
            var left = Geometry.FrozenWidth;

            if (bounds.X < Scroll.X + left) Scroll = Scroll.WithX(Math.Max(bounds.X - left, 0));
            else if (bounds.Right > Scroll.X + area.Width) Scroll = Scroll.WithX(Math.Max(bounds.Right - area.Width, 0));
        }

        if (address.Row >= Geometry.FrozenRows)
        {
            var top = Geometry.FrozenHeight;

            if (bounds.Y < Scroll.Y + top) Scroll = Scroll.WithY(Math.Max(bounds.Y - top, 0));
            else if (bounds.Bottom > Scroll.Y + area.Height) Scroll = Scroll.WithY(Math.Max(bounds.Bottom - area.Height, 0));
        }
    }

    public bool ScrollPane(Point point, Vector delta)
    {
        var inRows = InFrozenRows(point);
        var inColumns = InFrozenColumns(point);

        if (!inRows && !inColumns) return false;

        PaneScroll = new Vector(
            inColumns ? Math.Max(PaneScroll.X - delta.X * Geometry.DefaultColumnWidth, 0) : PaneScroll.X,
            inRows ? Math.Max(PaneScroll.Y - delta.Y * Metrics.RowHeight * 3, 0) : PaneScroll.Y);

        return true;
    }

    public void ScrollBy(Vector delta)
    {
        var (extentWidth, extentHeight) = Geometry.Extent(Sheet?.UsedRange());

        Scroll = new Vector(
            Math.Clamp(Scroll.X - delta.X * Geometry.DefaultColumnWidth, 0,
                Math.Max(extentWidth - GridArea().Width, 0)),
            Math.Clamp(Scroll.Y - delta.Y * Metrics.RowHeight * 3, 0,
                Math.Max(extentHeight - GridArea().Height, 0)));
    }
}

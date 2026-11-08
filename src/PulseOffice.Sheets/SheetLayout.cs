using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public sealed class SheetLayout
{
    private const int RowLimit = CellAddress.MaxRows - 1;
    private const int ColumnLimit = CellAddress.MaxColumns - 1;

    private readonly RunLengthMap<Twip> _columnWidths = new();
    private readonly RunLengthMap<Twip> _rowHeights = new();
    private readonly RunLengthMap<Twip> _automaticRowHeights = new();
    private readonly RunLengthMap<Twip> _effectiveRowHeights = new();
    private readonly RunLengthSet _optimalRows = new();
    private readonly RunLengthMap<Twip> _hiddenColumnSizes = new();
    private readonly RunLengthMap<Twip> _hiddenRowSizes = new();
    private readonly StyleBands _styles = new();
    private readonly AxisOffsets _columnOffsets = new();
    private readonly AxisOffsets _rowOffsets = new();

    public Twip DefaultColumnWidth { get; set; } = Twip.FromPoints(60);

    public Twip DefaultRowHeight { get; set; } = Twip.FromPoints(15);

    public bool DefaultRowHeightManual { get; set; }

    public event Action<SheetLayout>? RowVisibilityChanged;

    public event Action<SheetLayout>? GeometryChanging;

    public event Action<SheetLayout>? GeometryChanged;

    private int _geometryDepth;
    private int _geometrySuspensions;

    public IDisposable SuspendGeometryNotifications()
    {
        _geometrySuspensions++;
        return new GeometrySuspension(this);
    }

    private GeometryChange Geometry()
    {
        if (_geometryDepth++ == 0 && _geometrySuspensions == 0) GeometryChanging?.Invoke(this);
        return new GeometryChange(this);
    }

    private void EndGeometry()
    {
        if (--_geometryDepth == 0 && _geometrySuspensions == 0) GeometryChanged?.Invoke(this);
    }

    private sealed class GeometryChange(SheetLayout owner) : IDisposable
    {
        private bool _ended;

        public void Dispose()
        {
            if (_ended) return;
            _ended = true;
            owner.EndGeometry();
        }
    }

    private sealed class GeometrySuspension(SheetLayout owner) : IDisposable
    {
        private bool _ended;

        public void Dispose()
        {
            if (_ended) return;
            _ended = true;
            owner._geometrySuspensions--;
        }
    }

    private int _deferredNotifications;
    private readonly RunLengthSet _filteredRows = new();
    private readonly RunLengthSet _filterHiddenRows = new();
    private bool _visibilityPending;

    public IDisposable DeferRowVisibilityNotifications()
    {
        _deferredNotifications++;
        return new NotificationDeferral(this);
    }

    public void MarkFilteredRows(IEnumerable<int>? rows)
    {
        _filteredRows.Clear();
        foreach (var row in rows ?? []) _filteredRows.Add(row);
        RowVisibilityMayHaveChanged();
    }

    public IReadOnlyCollection<int> FilteredRows => _filteredRows;

    public IReadOnlyCollection<int> RowsHiddenByFilter => _filterHiddenRows;

    public void ApplyFilteredRows(IEnumerable<int>? rows, bool adoptHiddenRows = false)
    {
        using var geometry = Geometry();
        using var deferral = DeferRowVisibilityNotifications();

        foreach (var run in _filterHiddenRows.Runs.ToList()) ShowRows(run.First, run.Last);
        _filterHiddenRows.Clear();

        var marked = rows?.ToList();
        MarkFilteredRows(marked);
        if (marked is null) return;

        foreach (var row in marked)
        {
            if (IsRowHidden(row))
            {
                if (adoptHiddenRows) _filterHiddenRows.Add(row);
                continue;
            }

            HideRow(row);
            _filterHiddenRows.Add(row);
        }
    }

    public bool IsRowFiltered(int row) => _filteredRows.Contains(row) && IsRowHidden(row);

    private void EndDeferral()
    {
        if (--_deferredNotifications > 0 || !_visibilityPending) return;

        _visibilityPending = false;
        RowVisibilityChanged?.Invoke(this);
    }

    private void RowVisibilityMayHaveChanged()
    {
        if (_deferredNotifications > 0) _visibilityPending = true;
        else RowVisibilityChanged?.Invoke(this);
    }

    private List<(int First, int Last)> HiddenRuns(int first, int last) =>
        [.. _effectiveRowHeights.RunsIn(first, last).Where(run => run.Value.Value <= 0).Select(run => (run.First, run.Last))];

    public RunLengthMap<Twip> ColumnWidths => _columnWidths;

    public RunLengthMap<Twip> RowHeights => _rowHeights;

    public RunLengthMap<Twip> AutomaticRowHeights => _automaticRowHeights;

    public RunLengthSet OptimalRows => _optimalRows;

    public StyleBands Styles => _styles;

    public RunLengthMap<int> ColumnStyles => _styles.Columns;

    public RunLengthMap<int> RowStyles => _styles.Rows;

    public StyleBlocks StyleBlocks => _styles.Blocks;

    public Twip ColumnWidth(int column) =>
        _columnWidths.TryGetValue(column, out var width) ? width : DefaultColumnWidth;

    public Twip RowHeight(int row) =>
        _effectiveRowHeights.TryGetValue(row, out var height) ? height : DefaultRowHeight;

    public bool IsRowHeightManual(int row) => _rowHeights.ContainsKey(row);

    public bool IsRowAutomatic(int row) =>
        IsRowHidden(row)
            ? !_hiddenRowSizes.ContainsKey(row) && AutomaticByDefault(row)
            : IsRowOptimal(row);

    public bool IsRowOptimal(int row) => !_rowHeights.ContainsKey(row) && AutomaticByDefault(row);

    private bool AutomaticByDefault(int row) =>
        !DefaultRowHeightManual || _optimalRows.Contains(row) || _automaticRowHeights.ContainsKey(row);

    public void MakeRowOptimal(int row)
    {
        using var geometry = Geometry();
        _rowHeights.Remove(row);
        if (DefaultRowHeightManual) _optimalRows.Add(row);
        Recompose(row, row);
    }

    public void ClearRowOptimal(int row)
    {
        using var geometry = Geometry();
        _optimalRows.Remove(row);
        _automaticRowHeights.Remove(row);
        Recompose(row, row);
    }

    public void SetAutomaticRowHeight(int row, Twip? height) => SetAutomaticRowHeights(row, row, height);

    public void SetAutomaticRowHeights(int first, int last, Twip? height)
    {
        if (last < first) return;

        using var geometry = Geometry();

        if (DefaultRowHeightManual) _optimalRows.Add(first, last);
        if (height is { } size && size.Value > 0 && size != DefaultRowHeight) _automaticRowHeights.Set(first, last, size);
        else _automaticRowHeights.Remove(first, last);
        Recompose(first, last);
    }

    public void ClearAutomaticRowHeights()
    {
        using var geometry = Geometry();
        _automaticRowHeights.Clear();
        RecomposeAll();
    }

    public void SetColumnWidth(int column, Twip width) => SetColumnWidthRange(column, column, width);

    public void SetColumnWidthRange(int first, int last, Twip width)
    {
        using var geometry = Geometry();
        _columnWidths.Set(first, last, new Twip(Math.Max(width.Value, 0)));
    }

    public void SetRowHeight(int row, Twip height) => SetRowHeightRange(row, row, height);

    public void SetRowHeightRange(int first, int last, Twip height)
    {
        using var geometry = Geometry();
        _rowHeights.Set(first, last, new Twip(Math.Max(height.Value, 0)));
        _optimalRows.Remove(first, last);
        Recompose(first, last);
    }

    public void HideColumn(int column) => HideColumns(column, column);

    public void HideColumns(int first, int last)
    {
        if (last < first) return;

        using var geometry = Geometry();

        foreach (var segment in _columnWidths.Segments(first, last))
        {
            if (segment.Present ? segment.Value.Value <= 0 : DefaultColumnWidth.Value <= 0) continue;

            _hiddenColumnSizes.Remove(segment.First, segment.Last);
            if (segment.Present) _hiddenColumnSizes.Set(segment.First, segment.Last, segment.Value);
        }

        _columnWidths.Set(first, last, Twip.Zero);
    }

    public void ShowColumn(int column) => ShowColumns(column, column);

    public void ShowColumns(int first, int last)
    {
        using var geometry = Geometry();
        foreach (var hidden in _columnWidths.RunsIn(first, last).Where(run => run.Value.Value <= 0).ToList())
        {
            _columnWidths.Remove(hidden.First, hidden.Last);
            foreach (var kept in _hiddenColumnSizes.RunsIn(hidden.First, hidden.Last).ToList())
                _columnWidths.Set(kept.First, kept.Last, kept.Value);
            _hiddenColumnSizes.Remove(hidden.First, hidden.Last);
        }
    }

    public void HideRow(int row) => HideRows(row, row);

    public void HideRows(int first, int last)
    {
        if (last < first) return;

        using var geometry = Geometry();

        foreach (var segment in _effectiveRowHeights.Segments(first, last))
        {
            if (segment.Present ? segment.Value.Value <= 0 : DefaultRowHeight.Value <= 0) continue;

            _hiddenRowSizes.Remove(segment.First, segment.Last);
            foreach (var manual in _rowHeights.RunsIn(segment.First, segment.Last).ToList())
                _hiddenRowSizes.Set(manual.First, manual.Last, manual.Value);
        }

        _rowHeights.Set(first, last, Twip.Zero);
        Recompose(first, last);
    }

    public void ShowRow(int row) => ShowRows(row, row);

    public void ShowRows(int first, int last)
    {
        using var geometry = Geometry();
        foreach (var hidden in _rowHeights.RunsIn(first, last).Where(run => run.Value.Value <= 0).ToList())
        {
            _rowHeights.Remove(hidden.First, hidden.Last);
            foreach (var kept in _hiddenRowSizes.RunsIn(hidden.First, hidden.Last).ToList())
            {
                _rowHeights.Set(kept.First, kept.Last, kept.Value);
                _optimalRows.Remove(kept.First, kept.Last);
            }
            _hiddenRowSizes.Remove(hidden.First, hidden.Last);
            Recompose(hidden.First, hidden.Last);
        }
    }

    public void SetRowHeights(int first, int last, Twip height)
    {
        using var geometry = Geometry();
        if (first > 0 || last < RowLimit)
        {
            SetRowHeightRange(first, last, height);
            return;
        }

        var hidden = _rowHeights.Runs.Where(run => run.Value.Value <= 0).ToList();
        _rowHeights.Clear();
        foreach (var run in hidden)
        {
            _rowHeights.Set(run.First, run.Last, run.Value);
            _hiddenRowSizes.Remove(run.First, run.Last);
        }

        _automaticRowHeights.Clear();
        _optimalRows.Clear();
        var wasHidden = DefaultRowHeight.Value <= 0;
        DefaultRowHeight = new Twip(Math.Max(height.Value, 0));
        if (wasHidden != DefaultRowHeight.Value <= 0) RowVisibilityMayHaveChanged();
        DefaultRowHeightManual = true;
        RecomposeAll();
    }

    public void SetColumnWidths(int first, int last, Twip width)
    {
        using var geometry = Geometry();
        if (first > 0 || last < ColumnLimit)
        {
            SetColumnWidthRange(first, last, width);
            return;
        }

        var hidden = _columnWidths.Runs.Where(run => run.Value.Value <= 0).ToList();
        _columnWidths.Clear();
        foreach (var run in hidden)
        {
            _columnWidths.Set(run.First, run.Last, run.Value);
            _hiddenColumnSizes.Remove(run.First, run.Last);
        }

        DefaultColumnWidth = new Twip(Math.Max(width.Value, 0));
    }

    public int? ColumnStyle(int column) => _styles.Columns.TryGetValue(column, out var style) ? style : null;

    public int? RowStyle(int row) => _styles.Rows.TryGetValue(row, out var style) ? style : null;

    public void SetColumnStyle(int column, int? styleId) => _styles.SetColumns(column, column, styleId);

    public void SetColumnStyles(int first, int last, int? styleId) => _styles.SetColumns(first, last, styleId);

    public void SetRowStyle(int row, int? styleId) => _styles.SetRows(row, row, styleId);

    public void SetRowStyles(int first, int last, int? styleId) => _styles.SetRows(first, last, styleId);

    public void SetStyleBlock(CellRange area, int styleId) =>
        _styles.SetBlock(area.Start.Row, area.End.Row, area.Start.Column, area.End.Column, styleId);

    public int DefaultStyleAt(CellAddress address) => _styles.StyleAt(address);

    public void CopyTo(SheetLayout target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.DefaultColumnWidth = DefaultColumnWidth;
        target.DefaultRowHeight = DefaultRowHeight;
        target.DefaultRowHeightManual = DefaultRowHeightManual;

        _styles.CopyTo(target._styles);
        _columnWidths.CopyTo(target._columnWidths);
        _rowHeights.CopyTo(target._rowHeights);
        _automaticRowHeights.CopyTo(target._automaticRowHeights);
        var hidden = target.HiddenRuns(0, int.MaxValue);
        _effectiveRowHeights.CopyTo(target._effectiveRowHeights);
        if (!hidden.SequenceEqual(target.HiddenRuns(0, int.MaxValue))) target.RowVisibilityMayHaveChanged();
        _optimalRows.CopyTo(target._optimalRows);
        _filteredRows.CopyTo(target._filteredRows);
        _filterHiddenRows.CopyTo(target._filterHiddenRows);
        _hiddenColumnSizes.CopyTo(target._hiddenColumnSizes);
        _hiddenRowSizes.CopyTo(target._hiddenRowSizes);
    }

    public void ResetColumnWidth(int column)
    {
        using var geometry = Geometry();
        _columnWidths.Remove(column);
    }

    public void ResetRowHeight(int row)
    {
        using var geometry = Geometry();
        _rowHeights.Remove(row);
        Recompose(row, row);
    }

    public void InsertRows(int at, int count)
    {
        foreach (var map in RowMaps()) map.Insert(at, count, RowLimit);
        _optimalRows.Insert(at, count, RowLimit);
        _filteredRows.Insert(at, count, RowLimit);
        _filterHiddenRows.Insert(at, count, RowLimit);
        _styles.InsertRows(at, count, RowLimit);
        RecomposeAll();
    }

    public void DeleteRows(int at, int count)
    {
        foreach (var map in RowMaps()) map.Delete(at, count);
        _optimalRows.Delete(at, count);
        _filteredRows.Delete(at, count);
        _filterHiddenRows.Delete(at, count);
        _styles.DeleteRows(at, count);
        RecomposeAll();
    }

    public void InsertColumns(int at, int count)
    {
        _columnWidths.Insert(at, count, ColumnLimit);
        _hiddenColumnSizes.Insert(at, count, ColumnLimit);
        _styles.InsertColumns(at, count, ColumnLimit);
    }

    public void DeleteColumns(int at, int count)
    {
        _columnWidths.Delete(at, count);
        _hiddenColumnSizes.Delete(at, count);
        _styles.DeleteColumns(at, count);
    }

    private IEnumerable<RunLengthMap<Twip>> RowMaps() => [_rowHeights, _automaticRowHeights, _hiddenRowSizes];

    public Twip? HiddenRowSize(int row) =>
        IsRowHidden(row) && _hiddenRowSizes.TryGetValue(row, out var size) ? size : null;

    public bool IsColumnHidden(int column) => ColumnWidth(column).Value <= 0;

    public bool IsRowHidden(int row) => RowHeight(row).Value <= 0;

    public Twip ColumnOffset(int column) => _columnOffsets.Offset(_columnWidths, DefaultColumnWidth, column);

    public Twip RowOffset(int row) => _rowOffsets.Offset(_effectiveRowHeights, DefaultRowHeight, row);

    public int ShapeSignature()
    {
        var hash = new HashCode();
        hash.Add(DefaultRowHeight.Value);
        hash.Add(DefaultRowHeightManual);
        hash.Add(DefaultColumnWidth.Value);
        foreach (var run in _optimalRows.Runs) hash.Add(run);
        foreach (var run in _columnWidths.Runs) hash.Add(run);
        _styles.AddTo(ref hash);
        foreach (var run in _rowHeights.Runs) hash.Add(run);
        return hash.ToHashCode();
    }

    public CellRange? VisibleColumnsIn(CellRange range)
    {
        var first = -1;
        var last = -1;

        foreach (var segment in _columnWidths.Segments(range.Start.Column, range.End.Column))
        {
            if (segment.Present ? segment.Value.Value <= 0 : DefaultColumnWidth.Value <= 0) continue;

            if (first < 0) first = segment.First;
            last = segment.Last;
        }

        return first < 0
            ? null
            : new CellRange(
                new CellAddress(range.Start.Row, first),
                new CellAddress(range.End.Row, last));
    }

    private void Recompose(int first, int last)
    {
        var hidden = HiddenRuns(first, last);
        _effectiveRowHeights.Remove(first, last);
        foreach (var run in _automaticRowHeights.RunsIn(first, last).ToList()) _effectiveRowHeights.Set(run.First, run.Last, run.Value);
        foreach (var run in _rowHeights.RunsIn(first, last).ToList()) _effectiveRowHeights.Set(run.First, run.Last, run.Value);
        if (!hidden.SequenceEqual(HiddenRuns(first, last))) RowVisibilityMayHaveChanged();
    }

    private void RecomposeAll()
    {
        var hidden = HiddenRuns(0, int.MaxValue);
        _effectiveRowHeights.Clear();
        foreach (var run in _automaticRowHeights.Runs) _effectiveRowHeights.Set(run.First, run.Last, run.Value);
        foreach (var run in _rowHeights.Runs) _effectiveRowHeights.Set(run.First, run.Last, run.Value);
        if (!hidden.SequenceEqual(HiddenRuns(0, int.MaxValue))) RowVisibilityMayHaveChanged();
    }

    private sealed class NotificationDeferral(SheetLayout layout) : IDisposable
    {
        private bool _ended;

        public void Dispose()
        {
            if (_ended) return;

            _ended = true;
            layout.EndDeferral();
        }
    }

    private sealed class AxisOffsets
    {
        private long _revision = -1;
        private int _standard;
        private long[] _before = [];

        public Twip Offset(RunLengthMap<Twip> sizes, Twip standard, int index)
        {
            Refresh(sizes, standard);

            var runs = sizes.Runs;
            var low = 0;
            var high = runs.Count;
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (runs[middle].First < index) low = middle + 1;
                else high = middle;
            }

            var offset = (long)index * standard.Value;
            if (low > 0)
            {
                var previous = runs[low - 1];
                offset += _before[low - 1];
                var covered = Math.Min(previous.Last, index - 1) - previous.First + 1;
                offset += (long)covered * (previous.Value.Value - standard.Value);
            }

            return new Twip((int)Math.Clamp(offset, int.MinValue, int.MaxValue));
        }

        private void Refresh(RunLengthMap<Twip> sizes, Twip standard)
        {
            if (_revision == sizes.Revision && _standard == standard.Value && _before.Length == sizes.Runs.Count) return;

            var runs = sizes.Runs;
            _before = new long[runs.Count];
            long total = 0;
            for (var position = 0; position < runs.Count; position++)
            {
                _before[position] = total;
                total += (long)runs[position].Length * (runs[position].Value.Value - standard.Value);
            }

            _revision = sizes.Revision;
            _standard = standard.Value;
        }
    }
}

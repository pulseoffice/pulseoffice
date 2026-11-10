using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PulseOffice.Document.Embedding;

namespace PulseOffice.Data.UI;

public sealed class TableGridPanel : Panel
{
    private const int BodyLayer = 0;
    private const int CursorLayer = 1;
    private const int FrozenLayer = 2;
    private const int EditorLayer = 3;

    private readonly TableEditorSheet _sheet;
    private readonly TableEditorLayout _layout;
    private readonly ITableEditorPaletteSource _palettes;
    private readonly List<TableCellPresenter> _cells = [];
    private readonly List<TableCellPresenter> _frozen = [];
    private readonly Dictionary<Control, Rect> _placed = [];
    private readonly Border _cursor;
    private readonly TextBox _editor;
    private TableEditorPalette? _palette;
    private Size _viewport;
    private Vector _offset;
    private TableCellRef? _editing;
    private bool _dragging;

    public TableGridPanel(TableEditorSheet sheet, TableEditorLayout layout, ITableEditorPaletteSource palettes)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _palettes = palettes ?? throw new ArgumentNullException(nameof(palettes));

        Focusable = true;
        ClipToBounds = true;
        Background = Brushes.Transparent;

        _cursor = new Border { BorderThickness = new Thickness(2), IsHitTestVisible = false, ZIndex = CursorLayer, IsVisible = false };
        _editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = false,
            NewLine = "\n",
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(0),
            ZIndex = EditorLayer,
            IsVisible = false,
            FontSize = _layout.Metrics.FontSize,
            Padding = new Thickness(_layout.Metrics.PaddingX - 2, _layout.Metrics.PaddingY - 2),
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        _editor.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        _editor.LostFocus += (_, _) => Commit(refocus: false);
        Children.Add(_cursor);
        Children.Add(_editor);

        _sheet.Changed += OnSheetChanged;
        ActualThemeVariantChanged += (_, _) => Repaint();
    }

    public event EventHandler? ScrollChanged;

    public Size BodyViewport => new(
        Math.Max(0, _viewport.Width - _layout.Metrics.RowNumberWidth),
        Math.Max(0, _viewport.Height - _layout.FrozenTop));

    public Size BodyExtent => new(_layout.BodyWidth, _layout.BodyHeight);

    public bool IsEditing => _editing is not null;

    public Vector Offset
    {
        get => _offset;
        set
        {
            var clamped = Clamped(value);
            if (clamped == _offset) return;
            _offset = clamped;
            InvalidateMeasure();
            ScrollChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void BeginEdit(string? seed = null)
    {
        _editing = _sheet.Active;
        _editor.Text = seed ?? _sheet.Text(_sheet.Active);
        _editor.IsVisible = true;
        RefreshEditorLook();
        InvalidateMeasure();
        _editor.Focus();
        _editor.CaretIndex = _editor.Text?.Length ?? 0;
    }

    public void Commit() => Commit(refocus: true);

    private void Commit(bool refocus)
    {
        if (_editing is not { } cell) return;
        _editing = null;
        _editor.IsVisible = false;
        _sheet.SetText(cell, (_editor.Text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal));
        InvalidateMeasure();
        if (refocus) Focus();
    }

    public void Cancel()
    {
        if (_editing is null) return;
        _editing = null;
        _editor.IsVisible = false;
        InvalidateMeasure();
        Focus();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Repaint();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _palette ??= _palettes.Resolve(this);
        _viewport = new Size(
            double.IsInfinity(availableSize.Width) ? _layout.ExtentWidth : Math.Min(availableSize.Width, _layout.ExtentWidth),
            double.IsInfinity(availableSize.Height) ? _layout.ExtentHeight : Math.Min(availableSize.Height, _layout.ExtentHeight));
        _offset = Clamped(_offset);

        Realize(_palette);
        foreach (var (control, rect) in _placed) control.Measure(rect.Size);
        return _viewport;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (control, rect) in _placed) control.Arrange(rect);
        return finalSize;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is Visual source && (source == _editor || _editor.IsVisualAncestorOf(source))) return;
        Commit();
        Focus();

        var point = e.GetPosition(this);
        var metrics = _layout.Metrics;
        var extend = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        e.Handled = true;

        if (point.Y < metrics.LetterHeight)
        {
            if (point.X < metrics.RowNumberWidth) _sheet.SelectAll();
            else _sheet.SelectColumn(_layout.ColumnAt(point.X - metrics.RowNumberWidth + _offset.X));
            return;
        }

        if (point.X < metrics.RowNumberWidth)
        {
            if (point.Y < _layout.FrozenTop)
                _sheet.SelectRange(new TableCellRef(TableCellFormat.HeaderRow, 0), new TableCellRef(TableCellFormat.HeaderRow, _sheet.ColumnCount - 1));
            else _sheet.SelectRow(_layout.RowAt(point.Y - _layout.FrozenTop + _offset.Y));
            return;
        }

        _sheet.Select(CellAt(point), extend);
        if (e.ClickCount >= 2) { BeginEdit(); return; }
        _dragging = true;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        var cell = CellAt(e.GetPosition(this));
        if (cell != _sheet.Active) _sheet.Select(cell, extend: true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var step = _layout.Metrics.LineHeight * 3;
        var horizontal = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -e.Delta.Y : -e.Delta.X;
        Offset = _offset + new Vector(horizontal * step, e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 0 : -e.Delta.Y * step);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || IsEditing) return;

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        e.Handled = true;
        switch (e.Key)
        {
            case Key.Up: _sheet.Move(-1, 0, shift); break;
            case Key.Down: _sheet.Move(1, 0, shift); break;
            case Key.Left: _sheet.Move(0, -1, shift); break;
            case Key.Right: _sheet.Move(0, 1, shift); break;
            case Key.PageUp: _sheet.Move(-PageRows(), 0, shift); break;
            case Key.PageDown: _sheet.Move(PageRows(), 0, shift); break;
            case Key.Tab: _sheet.Advance(shift); break;
            case Key.Enter or Key.F2: BeginEdit(); break;
            case Key.Delete or Key.Back: _sheet.ClearSelection(); break;
            case Key.A when command: _sheet.SelectAll(); break;
            case Key.Z when command && shift: _sheet.Redo(); break;
            case Key.Z when command: _sheet.Undo(); break;
            case Key.Y when command: _sheet.Redo(); break;
            default: e.Handled = false; break;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || IsEditing || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0])) return;
        BeginEdit(e.Text);
        e.Handled = true;
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        switch (e.Key)
        {
            case Key.Escape:
                Cancel();
                e.Handled = true;
                break;
            case Key.Enter when command:
                Commit();
                _sheet.Move(1, 0);
                e.Handled = true;
                break;
            case Key.Tab:
                Commit();
                _sheet.Advance(e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                e.Handled = true;
                break;
        }
    }

    private void OnSheetChanged(object? sender, TableSheetChangedEventArgs e)
    {
        switch (e.Kind)
        {
            case TableSheetChangeKind.Structure: _layout.Recompute(); break;
            case TableSheetChangeKind.Text: _layout.Recompute(e.Rows); break;
        }
        _offset = Clamped(_offset);
        BringIntoView(_sheet.Active);
        InvalidateMeasure();
        ScrollChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Repaint()
    {
        _palette = _palettes.Resolve(this);
        RefreshEditorLook();
        InvalidateMeasure();
    }

    private void RefreshEditorLook()
    {
        if (_palette is null) return;
        _editor.Background = _palette.Surface;
        _editor.Foreground = _palette.Text;
        _editor.BorderBrush = _palette.Accent;
        _cursor.BorderBrush = _palette.Accent;
        _editor.FontWeight = _editing is { IsHeader: true } && _sheet.HeaderEmphasis ? FontWeight.SemiBold : FontWeight.Normal;
    }

    private void BringIntoView(TableCellRef cell)
    {
        if (cell.IsHeader) return;
        var (left, top, width, height) = _layout.CellBox(cell);
        var view = BodyViewport;
        var x = _offset.X;
        var y = _offset.Y;
        if (left < x) x = left;
        else if (left + width > x + view.Width) x = Math.Min(left, left + width - view.Width);
        if (top < y) y = top;
        else if (top + height > y + view.Height) y = Math.Min(top, top + height - view.Height);
        _offset = Clamped(new Vector(x, y));
    }

    private int PageRows() =>
        Math.Max(1, (int)(BodyViewport.Height / Math.Max(1, _layout.Metrics.MinRowHeight)) - 1);

    private Vector Clamped(Vector offset)
    {
        var view = BodyViewport;
        return new Vector(
            Math.Clamp(offset.X, 0, Math.Max(0, _layout.BodyWidth - view.Width)),
            Math.Clamp(offset.Y, 0, Math.Max(0, _layout.BodyHeight - view.Height)));
    }

    private TableCellRef CellAt(Point point)
    {
        var column = _layout.ColumnAt(point.X - _layout.Metrics.RowNumberWidth + _offset.X);
        var row = point.Y < _layout.FrozenTop ? TableCellFormat.HeaderRow : _layout.RowAt(point.Y - _layout.FrozenTop + _offset.Y);
        var cell = new TableCellRef(row, column);
        return _sheet.Index.Owner(cell) is { } owner ? new TableCellRef(owner.Row, owner.Column) : cell;
    }

    private void Realize(TableEditorPalette palette)
    {
        _placed.Clear();
        var metrics = _layout.Metrics;
        var left = metrics.RowNumberWidth;
        var top = _layout.FrozenTop;
        var view = BodyViewport;
        var selection = _sheet.Selection;

        var firstRow = _layout.RowAt(_offset.Y);
        var lastRow = _layout.RowAt(_offset.Y + Math.Max(0, view.Height - 1));
        var firstColumn = _layout.ColumnAt(_offset.X);
        var lastColumn = _layout.ColumnAt(_offset.X + Math.Max(0, view.Width - 1));

        var origins = new List<TableCellRef>();
        var seen = new HashSet<TableCellRef>();
        for (var row = firstRow; row <= lastRow && row < _sheet.RowCount; row++)
            for (var column = firstColumn; column <= lastColumn; column++)
                if (seen.Add(OriginOf(new TableCellRef(row, column)))) origins.Add(OriginOf(new TableCellRef(row, column)));

        var used = 0;
        foreach (var cell in origins)
        {
            var (x, y, width, height) = _layout.CellBox(cell);
            var presenter = Pooled(_cells, used++, BodyLayer);
            presenter.Bind(cell, _sheet.Text(cell), BodyLook(cell, palette, selection));
            Place(presenter, new Rect(left + x - _offset.X, top + y - _offset.Y, width, height));
        }
        Hide(_cells, used);

        var frozen = 0;
        var headerSeen = new HashSet<TableCellRef>();
        for (var column = firstColumn; column <= lastColumn; column++)
        {
            var cell = OriginOf(new TableCellRef(TableCellFormat.HeaderRow, column));
            if (!headerSeen.Add(cell)) continue;
            var (x, _, width, _) = _layout.CellBox(cell);
            var presenter = Pooled(_frozen, frozen++, FrozenLayer);
            presenter.Bind(cell, _sheet.Text(cell), HeaderLook(palette, selection.Contains(cell)));
            Place(presenter, new Rect(left + x - _offset.X, metrics.LetterHeight, width, _layout.HeaderHeight));
        }

        for (var column = firstColumn; column <= lastColumn; column++)
        {
            var presenter = Pooled(_frozen, frozen++, FrozenLayer + 1);
            var picked = column >= selection.FirstColumn && column <= selection.LastColumn;
            presenter.Bind(new TableCellRef(TableCellFormat.HeaderRow, column), ColumnLetter(column), ChromeLook(palette, picked, HorizontalAlignment.Center));
            Place(presenter, new Rect(left + _layout.ColumnLeft(column) - _offset.X, 0, metrics.ColumnWidth, metrics.LetterHeight));
        }

        for (var row = firstRow; row <= lastRow && row < _sheet.RowCount; row++)
        {
            var presenter = Pooled(_frozen, frozen++, FrozenLayer + 1);
            var picked = row >= selection.FirstRow && row <= selection.LastRow;
            presenter.Bind(new TableCellRef(row, -1), (row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), ChromeLook(palette, picked, HorizontalAlignment.Center));
            Place(presenter, new Rect(0, top + _layout.RowTop(row) - _offset.Y, left, _layout.RowHeight(row)));
        }

        var headerLabel = Pooled(_frozen, frozen++, FrozenLayer + 1);
        headerLabel.Bind(new TableCellRef(TableCellFormat.HeaderRow, -1), string.Empty, ChromeLook(palette, selection.FirstRow == TableCellFormat.HeaderRow, HorizontalAlignment.Center));
        Place(headerLabel, new Rect(0, metrics.LetterHeight, left, _layout.HeaderHeight));

        var corner = Pooled(_frozen, frozen++, FrozenLayer + 2);
        corner.Bind(new TableCellRef(TableCellFormat.HeaderRow, -1), string.Empty, ChromeLook(palette, false, HorizontalAlignment.Center));
        Place(corner, new Rect(0, 0, left, metrics.LetterHeight));
        Hide(_frozen, frozen);

        var active = Box(_sheet.Active, left, top);
        _cursor.IsVisible = _editing is null;
        _cursor.ZIndex = _sheet.Active.IsHeader ? FrozenLayer + 1 : CursorLayer;
        _cursor.BorderBrush = palette.Accent;
        if (_cursor.IsVisible) Place(_cursor, active);

        if (_editing is { } editing)
        {
            var box = Box(editing, left, top);
            _editor.Measure(new Size(box.Width, double.PositiveInfinity));
            Place(_editor, box.WithHeight(Math.Max(box.Height, _editor.DesiredSize.Height)));
        }
    }

    private Rect Box(TableCellRef cell, double left, double top)
    {
        var (x, y, width, height) = _layout.CellBox(cell);
        return cell.IsHeader
            ? new Rect(left + x - _offset.X, _layout.Metrics.LetterHeight, width, _layout.HeaderHeight)
            : new Rect(left + x - _offset.X, top + y - _offset.Y, width, height);
    }

    private TableCellRef OriginOf(TableCellRef cell) =>
        _sheet.Index.Owner(cell) is { } owner ? new TableCellRef(owner.Row, owner.Column) : cell;

    private TableCellAppearance BodyLook(TableCellRef cell, TableEditorPalette palette, TableSelection selection)
    {
        var metrics = _layout.Metrics;
        var background = selection.Contains(cell) ? palette.Selection
            : _sheet.BandedRows && cell.Row % 2 == 1 ? palette.Band
            : palette.Surface;
        return new TableCellAppearance(
            background, palette.Text, palette.GridLine, new Thickness(0, 0, 1, 1), FontWeight.Normal,
            HorizontalAlignment.Stretch, Vertical(_sheet.Index.VerticalAlign(cell)),
            new Thickness(metrics.PaddingX, metrics.PaddingY), metrics.FontSize);
    }

    private TableCellAppearance HeaderLook(TableEditorPalette palette, bool selected)
    {
        var metrics = _layout.Metrics;
        var emphasis = _sheet.HeaderEmphasis;
        return new TableCellAppearance(
            selected ? palette.Selection : emphasis ? palette.HeaderField : palette.Surface,
            emphasis ? palette.Text : palette.MutedText,
            emphasis ? palette.Accent : palette.GridLine,
            new Thickness(0, 0, 1, emphasis ? 2 : 1),
            emphasis ? FontWeight.SemiBold : FontWeight.Normal,
            HorizontalAlignment.Stretch, VerticalAlignment.Top,
            new Thickness(metrics.PaddingX, metrics.PaddingY), metrics.FontSize);
    }

    private TableCellAppearance ChromeLook(TableEditorPalette palette, bool picked, HorizontalAlignment horizontal) => new(
        picked ? palette.SelectedChrome : palette.Chrome,
        picked ? palette.Accent : palette.MutedText,
        palette.GridLine, new Thickness(0, 0, 1, 1), FontWeight.SemiBold,
        horizontal, VerticalAlignment.Center, new Thickness(2, 0), _layout.Metrics.FontSize - 1);

    private static VerticalAlignment Vertical(CellVerticalAlign align) => align switch
    {
        CellVerticalAlign.Center => VerticalAlignment.Center,
        CellVerticalAlign.Bottom => VerticalAlignment.Bottom,
        _ => VerticalAlignment.Top,
    };

    private TableCellPresenter Pooled(List<TableCellPresenter> pool, int index, int layer)
    {
        if (index >= pool.Count)
        {
            var presenter = new TableCellPresenter();
            pool.Add(presenter);
            Children.Add(presenter);
        }
        pool[index].ZIndex = layer;
        return pool[index];
    }

    private void Place(Control control, Rect rect) => _placed[control] = rect;

    private static void Hide(List<TableCellPresenter> pool, int used)
    {
        for (var index = used; index < pool.Count; index++) pool[index].IsVisible = false;
    }

    private static string ColumnLetter(int index)
    {
        var letters = string.Empty;
        for (var n = index; n >= 0; n = n / 26 - 1)
            letters = (char)('A' + n % 26) + letters;
        return letters;
    }
}

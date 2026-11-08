using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Document.Embedding;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Data.UI;

public sealed record TableEdit(
    DataSnapshot Snapshot,
    IReadOnlyList<TableMerge> Merges,
    IReadOnlyList<TableCellFormat> CellFormats);

public sealed class TableEditorView : UserControl, IDialogContent, IDialogFooterOwner
{
    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    private const double HeaderColumnWidth = 40;
    private const double DataColumnWidth = 132;

    private readonly ITextCatalog _texts;
    private readonly ITableEditModel _model;
    private readonly DataSnapshot _original;
    private readonly DesignTokens.Metrics _metrics;
    private readonly List<string> _headers = [];
    private readonly List<List<string>> _rows = [];
    private readonly List<TableMerge> _merges = [];
    private readonly List<TableCellFormat> _cellFormats = [];
    private readonly Grid _grid;

    private (int Row, int Col) _selStart = (0, 0);
    private (int Row, int Col) _selEnd = (0, 0);

    private readonly Dictionary<int, TextBox> _headerBoxes = [];
    private readonly Dictionary<(int, int), TableMerge> _origins = [];
    private readonly HashSet<(int, int)> _covered = [];
    private readonly Dictionary<(int, int), TextBox> _cellBoxes = [];
    private readonly Dictionary<(int, int), Border> _cellFrames = [];

    private Button _mergeButton = null!;
    private Button _splitButton = null!;

    private IBrush _surface = Brushes.White;
    private IBrush _band = Brushes.WhiteSmoke;
    private IBrush _gridLine = Brushes.Gainsboro;
    private IBrush _headerFill = Brushes.WhiteSmoke;
    private IBrush _accent = Brushes.SteelBlue;
    private IBrush _selectionFill = new SolidColorBrush(Color.FromArgb(0x33, 0x25, 0x63, 0xEB));
    private IBrush _fieldFill = new SolidColorBrush(Color.FromArgb(0x1A, 0x25, 0x63, 0xEB));

    public TableEditorView(DataTableObject table, ITextCatalog texts, ITableEditModel? model = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _model = model ?? new TableEditModel();
        _original = table.Snapshot;
        _metrics = DesignTokens.For(InterfaceDensity.Compact);

        var snapshot = table.Snapshot;
        foreach (var column in snapshot.Columns) _headers.Add(column.Name);
        foreach (var row in snapshot.Rows)
            _rows.Add([.. Enumerable.Range(0, _headers.Count).Select(i =>
                i < row.Count ? _model.Display(row[i]) : string.Empty)]);
        _merges.AddRange(table.Merges);
        _cellFormats.AddRange(table.CellFormats);

        if (_headers.Count == 0) { _headers.Add(Column(1)); _rows.Add([string.Empty]); }
        if (_rows.Count == 0) _rows.Add([.. _headers.Select(_ => string.Empty)]);

        _grid = new Grid();

        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new ScrollViewer
            {
                Content = _grid,
                MaxHeight = 340,
                MaxWidth = 620,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            },
        }
        .Themed(Border.BorderBrushProperty, Palette.Keys.BorderStrong)
        .Themed(Border.BackgroundProperty, Palette.Keys.Surface);

        Content = new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            Children =
            {
                new TextBlock
                {
                    Text = _texts["deck.table.selectHint"],
                    FontSize = _metrics.FontSizeSmall,
                    TextWrapping = TextWrapping.Wrap,
                }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                frame,
                BuildToolbar(),
            },
        };

        Rebuild();
        ActualThemeVariantChanged += (_, _) => Refresh();

        Footer = new DialogFooter(InterfaceDensity.Compact)
            .WithCancel(_texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(_texts["prompt.confirm"], () => Completed?.Invoke(this, Build()), primary: true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
    }

    private void Refresh()
    {
        Capture();
        Rebuild();
    }

    private void IndexMerges()
    {
        _origins.Clear();
        _covered.Clear();
        foreach (var merge in _merges)
        {
            _origins.TryAdd((merge.Row, merge.Column), merge);
            for (var row = merge.Row; row < merge.Row + merge.RowSpan && row < _rows.Count; row++)
                for (var column = merge.Column; column < merge.Column + merge.ColumnSpan && column < _headers.Count; column++)
                    if (merge.Covers(row, column)) _covered.Add((row, column));
        }
    }

    private void ResolveBrushes()
    {
        _surface = Brush(Palette.Keys.Surface, Brushes.White);
        _band = Brush(Palette.Keys.SurfaceSunken, Brushes.WhiteSmoke);
        _gridLine = Brush(Palette.Keys.Border, Brushes.Gainsboro);
        _headerFill = Brush(Palette.Keys.SurfaceRaised, Brushes.WhiteSmoke);
        _accent = this.FindResource("PulseAccent") as IBrush ?? Brushes.SteelBlue;

        var accentColor = (_accent as ISolidColorBrush)?.Color ?? Color.FromRgb(0x25, 0x63, 0xEB);
        _selectionFill = new SolidColorBrush(accentColor, 0.22);
        _fieldFill = new SolidColorBrush(accentColor, 0.10);
    }

    private IBrush Brush(string key, IBrush fallback) => this.FindResource(key) as IBrush ?? fallback;

    private void Rebuild()
    {
        ResolveBrushes();

        _grid.Children.Clear();
        _grid.ColumnDefinitions.Clear();
        _grid.RowDefinitions.Clear();
        _headerBoxes.Clear();
        _cellBoxes.Clear();
        _cellFrames.Clear();
        IndexMerges();

        _grid.ColumnDefinitions.Add(new ColumnDefinition(HeaderColumnWidth, GridUnitType.Pixel));
        for (var c = 0; c < _headers.Count; c++)
            _grid.ColumnDefinitions.Add(new ColumnDefinition(DataColumnWidth, GridUnitType.Pixel));

        _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var r = 0; r < _rows.Count; r++)
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Place(CornerCell(), 0, 0);
        for (var c = 0; c < _headers.Count; c++)
            Place(ColumnHeaderCell(c), 0, c + 1);

        Place(RowHeaderCell(string.Empty, selectable: false, r: -1), 1, 0);
        for (var c = 0; c < _headers.Count; c++)
        {
            if (Covered(TableCellFormat.HeaderRow, c)) continue;
            var (rowSpan, colSpan) = SpanOf(TableCellFormat.HeaderRow, c);
            var (box, host) = FieldCell(_headers[c]);
            _headerBoxes[c] = box;
            Place(host, 1, c + 1, rowSpan, colSpan);
        }

        for (var r = 0; r < _rows.Count; r++)
        {
            Place(RowHeaderCell((r + 1).ToString(), selectable: true, r), r + 2, 0);
            for (var c = 0; c < _headers.Count; c++)
            {
                if (Covered(r, c)) continue;
                var (rowSpan, colSpan) = SpanOf(r, c);
                var (box, host) = BodyCell(r, c);
                _cellBoxes[(r, c)] = box;
                _cellFrames[(r, c)] = host;
                Place(host, r + 2, c + 1, rowSpan, colSpan);
            }
        }

        Highlight();
    }

    private void Place(Control control, int row, int col, int rowSpan = 1, int colSpan = 1)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, col);
        if (rowSpan > 1) Grid.SetRowSpan(control, rowSpan);
        if (colSpan > 1) Grid.SetColumnSpan(control, colSpan);
        _grid.Children.Add(control);
    }

    private Border CornerCell()
    {
        var border = new Border
        {
            Background = _headerFill,
            BorderBrush = _gridLine,
            BorderThickness = new Thickness(0, 0, 1, 1),
            MinHeight = _metrics.RowHeight,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        border.AddHandler(PointerPressedEvent, (_, _) => SelectAll(), RoutingStrategies.Tunnel);
        return border;
    }

    private void SelectAll()
    {
        _selStart = (0, 0);
        _selEnd = (Math.Max(0, _rows.Count - 1), Math.Max(0, _headers.Count - 1));
        Highlight();
    }

    private void SetVerticalAlign(CellVerticalAlign align)
    {
        Capture();
        var (r1, r2, c1, c2) = Selection();
        var changed = _model.WithVerticalAlign(_cellFormats, r1, r2, c1, c2, align);
        _cellFormats.Clear();
        _cellFormats.AddRange(changed);
    }

    private Border ColumnHeaderCell(int c)
    {
        var border = new Border
        {
            Background = _headerFill,
            BorderBrush = _gridLine,
            BorderThickness = new Thickness(0, 0, 1, 1),
            MinHeight = _metrics.RowHeight,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new TextBlock
            {
                Text = ColumnLetter(c),
                FontSize = _metrics.FontSizeSmall,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush(Palette.Keys.TextSecondary, Brushes.Gray),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        border.AddHandler(PointerPressedEvent, (_, _) => SelectColumn(c), RoutingStrategies.Tunnel);
        return border;
    }

    private Border RowHeaderCell(string label, bool selectable, int r)
    {
        var border = new Border
        {
            Background = _headerFill,
            BorderBrush = _gridLine,
            BorderThickness = new Thickness(0, 0, 1, 1),
            MinHeight = _metrics.RowHeight,
            Cursor = selectable ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            Padding = new Thickness(_metrics.SpacingSmall, 0),
            Child = new TextBlock
            {
                Text = label,
                FontSize = _metrics.FontSizeSmall,
                FontWeight = selectable ? FontWeight.SemiBold : FontWeight.Normal,
                Foreground = Brush(Palette.Keys.TextSecondary, Brushes.Gray),
                HorizontalAlignment = selectable ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        };
        if (selectable) border.AddHandler(PointerPressedEvent, (_, _) => SelectRow(r), RoutingStrategies.Tunnel);
        return border;
    }

    private (TextBox Box, Border Host) FieldCell(string value)
    {
        var box = CellBox(value, FontWeight.SemiBold);
        var host = new Border
        {
            Background = _fieldFill,
            BorderBrush = _gridLine,
            BorderThickness = new Thickness(0, 0, 1, 2),
            Child = box,
        };
        return (box, host);
    }

    private (TextBox Box, Border Host) BodyCell(int r, int c)
    {
        var box = CellBox(c < _rows[r].Count ? _rows[r][c] : string.Empty, FontWeight.Normal);
        var host = new Border
        {
            BorderBrush = _gridLine,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = box,
        };
        var rr = r;
        var cc = c;
        host.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) _selEnd = (rr, cc);
            else { _selStart = (rr, cc); _selEnd = (rr, cc); }
            Highlight();
        }, RoutingStrategies.Tunnel);
        return (box, host);
    }

    private TextBox CellBox(string value, FontWeight weight) => new()
    {
        Text = value,
        FontWeight = weight,
        FontSize = _metrics.FontSize,
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(0),
        Padding = new Thickness(_metrics.SpacingSmall, _metrics.SpacingSmall / 2),
        MinHeight = _metrics.RowHeight,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private void Highlight()
    {
        var (r1, r2, c1, c2) = Selection();
        foreach (var ((r, c), frame) in _cellFrames)
        {
            var selected = r >= r1 && r <= r2 && c >= c1 && c <= c2;
            var banded = r % 2 == 1;
            frame.Background = selected ? _selectionFill : banded ? _band : _surface;
        }

        _mergeButton.IsEnabled = r1 != r2 || c1 != c2;
        _splitButton.IsEnabled = _merges.Any(m => m.IsOrigin(r1, c1) || m.Covers(r1, c1));
    }

    private (int R1, int R2, int C1, int C2) Selection() =>
        (Math.Min(_selStart.Row, _selEnd.Row), Math.Max(_selStart.Row, _selEnd.Row),
         Math.Min(_selStart.Col, _selEnd.Col), Math.Max(_selStart.Col, _selEnd.Col));

    private void SelectColumn(int c)
    {
        _selStart = (0, c);
        _selEnd = (_rows.Count - 1, c);
        Highlight();
    }

    private void SelectRow(int r)
    {
        _selStart = (r, 0);
        _selEnd = (r, _headers.Count - 1);
        Highlight();
    }

    private bool Covered(int r, int c) => _covered.Contains((r, c));

    private (int RowSpan, int ColSpan) SpanOf(int r, int c) =>
        _origins.TryGetValue((r, c), out var origin)
            ? (Math.Clamp(origin.RowSpan, 1, _rows.Count - r), Math.Clamp(origin.ColumnSpan, 1, _headers.Count - c))
            : (1, 1);

    private void MergeSelection()
    {
        Capture();
        var (r1, r2, c1, c2) = Selection();
        if (r1 == r2 && c1 == c2) return;

        _merges.RemoveAll(m => m.Row <= r2 && m.Row + m.RowSpan - 1 >= r1 && m.Column <= c2 && m.Column + m.ColumnSpan - 1 >= c1);
        _merges.Add(new TableMerge(r1, c1, r2 - r1 + 1, c2 - c1 + 1));
        _selStart = _selEnd = (r1, c1);
        Rebuild();
    }

    private void SplitSelection()
    {
        Capture();
        var (r1, _, c1, _) = Selection();
        _merges.RemoveAll(m => m.IsOrigin(r1, c1) || m.Covers(r1, c1));
        Rebuild();
    }

    private void TrimRow(int removed)
    {
        _cellFormats.RemoveAll(f => f.Row >= removed);
        for (var i = _merges.Count - 1; i >= 0; i--)
        {
            var merge = _merges[i];
            var span = Math.Min(merge.RowSpan, removed - merge.Row);
            if (span <= 0 || (span == 1 && merge.ColumnSpan == 1)) _merges.RemoveAt(i);
            else _merges[i] = merge with { RowSpan = span };
        }
    }

    private void TrimColumn(int removed)
    {
        _cellFormats.RemoveAll(f => f.Column >= removed);
        for (var i = _merges.Count - 1; i >= 0; i--)
        {
            var merge = _merges[i];
            var span = Math.Min(merge.ColumnSpan, removed - merge.Column);
            if (span <= 0 || (span == 1 && merge.RowSpan == 1)) _merges.RemoveAt(i);
            else _merges[i] = merge with { ColumnSpan = span };
        }
    }

    private void Reformat(IReadOnlyList<TableCellFormat> formats)
    {
        var kept = formats.ToList();
        _cellFormats.Clear();
        _cellFormats.AddRange(kept);
    }

    private Control BuildToolbar()
    {
        var rows = Group(_texts["deck.table.group.rows"],
            ToolButton("＋", _texts["command.deck.table.addRow.title"], () =>
            {
                Capture(); _rows.Add([.. _headers.Select(_ => string.Empty)]); Reformat(_model.WithAppendedRow(_cellFormats, _rows.Count - 1)); Rebuild();
            }),
            ToolButton("－", _texts["command.deck.table.removeRow.title"], () =>
            {
                if (_rows.Count > 1) { Capture(); _rows.RemoveAt(_rows.Count - 1); TrimRow(_rows.Count); ClampSelection(); Rebuild(); }
            }));

        var columns = Group(_texts["deck.table.group.columns"],
            ToolButton("＋", _texts["command.deck.table.addColumn.title"], () =>
            {
                Capture(); _headers.Add(Column(_headers.Count + 1)); foreach (var row in _rows) row.Add(string.Empty); Reformat(_model.WithAppendedColumn(_cellFormats, _headers.Count - 1)); Rebuild();
            }),
            ToolButton("－", _texts["command.deck.table.removeColumn.title"], () =>
            {
                if (_headers.Count > 1) { Capture(); _headers.RemoveAt(_headers.Count - 1); foreach (var row in _rows) row.RemoveAt(row.Count - 1); TrimColumn(_headers.Count); ClampSelection(); Rebuild(); }
            }));

        _mergeButton = ToolButton("⊞", _texts["command.deck.table.merge.title"], MergeSelection);
        _splitButton = ToolButton("⊟", _texts["command.deck.table.split.title"], SplitSelection);
        var cells = Group(_texts["deck.table.group.cells"], _mergeButton, _splitButton);

        var align = Group(_texts["deck.table.group.align"],
            ToolButton("⤒", _texts["command.deck.table.valignTop.title"], () => SetVerticalAlign(CellVerticalAlign.Top)),
            ToolButton("⛶", _texts["command.deck.table.valignCenter.title"], () => SetVerticalAlign(CellVerticalAlign.Center)),
            ToolButton("⤓", _texts["command.deck.table.valignBottom.title"], () => SetVerticalAlign(CellVerticalAlign.Bottom)));

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingLarge,
            Children = { rows, columns, cells, align },
        };
    }

    private Control Group(string caption, params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall };
        foreach (var b in buttons) row.Children.Add(b);

        return new StackPanel
        {
            Spacing = _metrics.SpacingSmall / 2,
            Children =
            {
                new TextBlock
                {
                    Text = caption.ToUpperInvariant(),
                    FontSize = _metrics.FontSizeSmall - 1,
                    FontWeight = FontWeight.SemiBold,
                    LetterSpacing = 0.8,
                }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                row,
            },
        };
    }

    private Button ToolButton(string glyph, string label, Action run)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
            Children =
            {
                new TextBlock { Text = glyph, FontSize = _metrics.FontSize, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = label, FontSize = _metrics.FontSizeSmall, VerticalAlignment = VerticalAlignment.Center },
            },
        };

        var button = new Button
        {
            Content = content,
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            BorderThickness = new Thickness(1),
        }
        .Themed(Button.BackgroundProperty, Palette.Keys.Surface)
        .Themed(Button.BorderBrushProperty, Palette.Keys.Border)
        .Themed(Button.ForegroundProperty, Palette.Keys.TextPrimary);

        button.Click += (_, _) => run();
        return button;
    }

    private void ClampSelection()
    {
        var lastRow = Math.Max(0, _rows.Count - 1);
        var lastCol = Math.Max(0, _headers.Count - 1);
        _selStart = (Math.Min(_selStart.Row, lastRow), Math.Min(_selStart.Col, lastCol));
        _selEnd = (Math.Min(_selEnd.Row, lastRow), Math.Min(_selEnd.Col, lastCol));
    }

    private void Capture()
    {
        foreach (var (c, box) in _headerBoxes)
            if (c < _headers.Count) _headers[c] = box.Text ?? string.Empty;
        foreach (var ((r, c), box) in _cellBoxes)
            if (r < _rows.Count && c < _rows[r].Count)
                _rows[r][c] = box.Text ?? string.Empty;
    }

    private TableEdit Build()
    {
        Capture();
        return new TableEdit(
            _model.Compose(_original, _headers, _rows),
            [.. _merges],
            [.. _cellFormats]);
    }

    private string Column(int index) => $"{_texts["deck.table.column"]} {index}";

    private static string ColumnLetter(int index)
    {
        var letters = string.Empty;
        for (var n = index; n >= 0; n = n / 26 - 1)
            letters = (char)('A' + n % 26) + letters;
        return letters;
    }
}

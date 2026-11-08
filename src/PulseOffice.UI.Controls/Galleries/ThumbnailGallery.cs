using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Galleries;

public sealed class ThumbnailGallery<T> : UserControl
{
    private readonly IReadOnlyList<ThumbnailGalleryItem<T>> _items;
    private readonly IThumbnailPainter<T> _painter;
    private readonly IGalleryNavigator _navigator;
    private readonly ThumbnailGalleryOptions _options;
    private readonly DesignTokens.Metrics _metrics;
    private readonly Dictionary<int, (Button Tile, Control Thumbnail)> _realized = [];
    private readonly ContentControl _host = new();
    private readonly TextBox? _filter;
    private readonly TextBlock _empty = new();
    private List<int> _visible = [];
    private List<GallerySection> _sections = [];
    private IReadOnlyList<GalleryRow> _rows = [];
    private ItemsControl? _virtualRows;
    private int _virtualColumns;
    private int _selected = -1;

    public ThumbnailGallery(
        IReadOnlyList<ThumbnailGalleryItem<T>> items,
        IThumbnailPainter<T> painter,
        IGalleryNavigator navigator,
        ThumbnailGalleryOptions options,
        DesignTokens.Metrics metrics)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _painter = painter ?? throw new ArgumentNullException(nameof(painter));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _metrics = metrics;

        if (options.AccessibleName.Length > 0) Avalonia.Automation.AutomationProperties.SetName(this, options.AccessibleName);
        AddHandler(KeyDownEvent, Keys, RoutingStrategies.Tunnel);

        _empty.Text = options.EmptyText;
        _empty.FontSize = metrics.FontSize;
        _empty.Margin = new Thickness(0, metrics.SpacingMedium);
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        if (options.Filterable)
        {
            _filter = new TextBox { PlaceholderText = options.FilterPlaceholder, Margin = new Thickness(0, 0, 0, metrics.SpacingSmall) };
            Avalonia.Automation.AutomationProperties.SetName(_filter, options.FilterPlaceholder);
            _filter.TextChanged += (_, _) => Rebuild();
            var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
            Grid.SetRow(_host, 1);
            layout.Children.Add(_filter);
            layout.Children.Add(_host);
            Content = layout;
        }
        else Content = _host;

        Rebuild();
    }

    public event EventHandler<T>? SelectionChanged;

    public event EventHandler<T>? Invoked;

    public IReadOnlyList<ThumbnailGalleryItem<T>> Items => _items;

    public int SelectedIndex => _selected;

    public T? SelectedItem => _selected >= 0 ? _items[_selected].Value : default;

    public bool IsVirtualizing => _items.Count > _options.VirtualizeAbove;

    public IReadOnlyList<int> VisibleIndices => _visible;

    public IReadOnlyList<Control> Thumbnails => [.. _realized.OrderBy(entry => entry.Key).Select(entry => entry.Value.Thumbnail)];

    public IReadOnlyList<Button> Tiles => [.. _realized.OrderBy(entry => entry.Key).Select(entry => entry.Value.Tile)];

    public string FilterText
    {
        get => _filter?.Text ?? string.Empty;
        set
        {
            if (_filter is not null) _filter.Text = value;
        }
    }

    public int Columns => _options.Columns ?? MeasuredColumns();

    public Button? TileOf(int index) => _realized.TryGetValue(index, out var entry) ? entry.Tile : null;

    public void Select(int index)
    {
        _selected = index >= 0 && index < _items.Count ? index : -1;
        foreach (var (key, entry) in _realized) entry.Tile.Classes.Set("checked", key == _selected);
    }

    public void Select(T item) => Select(IndexOf(item));

    public int IndexOf(T item)
    {
        for (var i = 0; i < _items.Count; i++)
            if (EqualityComparer<T>.Default.Equals(_items[i].Value, item)) return i;
        return -1;
    }

    public void RefreshThumbnails()
    {
        foreach (var (index, entry) in _realized) _painter.Repaint(entry.Thumbnail, _items[index].Value);
    }

    public bool HandleKey(Key key)
    {
        GalleryMove? move = key switch
        {
            Key.Left => GalleryMove.Left,
            Key.Right => GalleryMove.Right,
            Key.Up => GalleryMove.Up,
            Key.Down => GalleryMove.Down,
            Key.Home => GalleryMove.First,
            Key.End => GalleryMove.Last,
            _ => null,
        };
        return move is { } step && Navigate(step);
    }

    public bool Navigate(GalleryMove move)
    {
        if (_visible.Count == 0) return false;
        var current = _visible.IndexOf(FocusedTileIndex() ?? _selected);
        var target = _navigator.Target([.. _sections.Select(section => section.Positions.Count)], Columns, current, move);
        if (target < 0) return false;
        Choose(_visible[target], focus: true);
        return true;
    }

    private int? FocusedTileIndex()
    {
        foreach (var (index, entry) in _realized)
            if (entry.Tile.IsFocused) return index;
        return null;
    }

    public void Invoke()
    {
        if (_selected >= 0) Invoked?.Invoke(this, _items[_selected].Value);
    }

    private void Choose(int index, bool focus)
    {
        var changed = index != _selected;
        Select(index);
        if (focus) FocusTile(index);
        if (changed) SelectionChanged?.Invoke(this, _items[index].Value);
    }

    private void FocusTile(int index)
    {
        if (_virtualRows is not null && !_realized.ContainsKey(index))
        {
            var row = RowOf(index);
            if (row >= 0) _virtualRows.ScrollIntoView(row);
            _virtualRows.UpdateLayout();
        }
        if (_realized.TryGetValue(index, out var entry))
        {
            entry.Tile.Focus(NavigationMethod.Directional);
            entry.Tile.BringIntoView();
        }
    }

    private int RowOf(int index)
    {
        var position = _visible.IndexOf(index);
        for (var i = 0; i < _rows.Count; i++)
            if (_rows[i].Positions.Contains(position)) return i;
        return -1;
    }

    private void Keys(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None) return;
        if (e.Source is TextBox)
        {
            if (e.Key == Key.Down && _visible.Count > 0)
            {
                Choose(_visible.Contains(_selected) ? _selected : _visible[0], focus: true);
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Enter)
        {
            Invoke();
            e.Handled = _selected >= 0;
            return;
        }

        if (HandleKey(e.Key)) e.Handled = true;
    }

    private void Rebuild()
    {
        var filter = FilterText.Trim();
        var groups = Enumerable.Range(0, _items.Count).Where(index => Matches(_items[index], filter)).GroupBy(index => _items[index].Group).ToList();
        _visible = [.. groups.SelectMany(group => group)];
        _sections = [];
        var start = 0;
        foreach (var group in groups)
        {
            var size = group.Count();
            _sections.Add(new GallerySection(group.Key, [.. Enumerable.Range(start, size)]));
            start += size;
        }
        _realized.Clear();
        _virtualRows = null;

        if (_visible.Count == 0)
        {
            _host.Content = _empty;
            return;
        }

        _host.Content = IsVirtualizing ? VirtualLayout() : WrappedLayout();
    }

    private bool Matches(ThumbnailGalleryItem<T> item, string filter) =>
        filter.Length == 0
        || CultureInfo.CurrentCulture.CompareInfo.IndexOf(item.Caption, filter, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0
        || (item.Keywords is { } keywords && CultureInfo.CurrentCulture.CompareInfo.IndexOf(keywords, filter, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    private double Spacing => _options.Spacing ?? _metrics.SpacingSmall;

    private Control WrappedLayout()
    {
        Control body;
        if (_sections.All(section => section.Header is null))
            body = Wrap(_sections.SelectMany(section => section.Positions));
        else
        {
            var stack = new StackPanel { Spacing = _metrics.SpacingSmall };
            foreach (var section in _sections)
            {
                if (section.Header is { } header) stack.Children.Add(Header(header));
                stack.Children.Add(Wrap(section.Positions));
            }
            body = stack;
        }

        return _options.Scrolls ? Scroller(body) : body;
    }

    private WrapPanel Wrap(IEnumerable<int> positions)
    {
        var wrap = new WrapPanel { ItemSpacing = Spacing, LineSpacing = Spacing };
        if (_options.Columns is { } columns)
        {
            wrap.MaxWidth = columns * (_options.TileWidth + Spacing);
            wrap.HorizontalAlignment = HorizontalAlignment.Left;
        }
        foreach (var position in positions) wrap.Children.Add(Tile(_visible[position]));
        return wrap;
    }

    private ScrollViewer Scroller(Control content) => new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Padding = new Thickness(0, 0, _metrics.SpacingMedium, 0),
        Content = content,
    };

    private Control VirtualLayout()
    {
        _virtualColumns = Math.Max(1, _options.Columns ?? MeasuredColumns());
        _rows = _navigator.Rows(_sections, _virtualColumns);
        _virtualRows = new ItemsControl
        {
            ItemsSource = _rows,
            ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<GalleryRow>((row, _) => Row(row), supportsRecycling: false),
        };
        var scroller = Scroller(_virtualRows);
        scroller.MaxHeight = _options.MaxViewportHeight;
        scroller.SizeChanged += (_, _) =>
        {
            if (_options.Columns is null && MeasuredColumns() != _virtualColumns) Rebuild();
        };
        return scroller;
    }

    private Control Row(GalleryRow? row)
    {
        if (row is null) return new Panel();
        if (row.Header is { } header) return Header(header);
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Spacing, Margin = new Thickness(0, 0, 0, Spacing) };
        foreach (var position in row.Positions) panel.Children.Add(Tile(_visible[position]));
        return panel;
    }

    private int MeasuredColumns()
    {
        var width = Bounds.Width > 0 ? Bounds.Width - _metrics.SpacingMedium : 0;
        return width <= 0 ? 1 : Math.Max(1, (int)Math.Floor((width + Spacing) / (_options.TileWidth + Spacing)));
    }

    private TextBlock Header(string text) => new TextBlock
    {
        Text = text.ToUpperInvariant(),
        FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
        LetterSpacing = 0.6,
        Margin = new Thickness(0, _metrics.SpacingSmall, 0, 2),
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private Button Tile(int index)
    {
        var item = _items[index];
        var padding = _options.TilePadding;
        var width = double.IsNaN(_options.ThumbnailWidth)
            ? _options.TileWidth - padding.Left - padding.Right - (_options.FrameBorder ? 2 : 0)
            : _options.ThumbnailWidth;
        var thumbnail = _painter.Create(item.Value, new Size(width, _options.ThumbnailHeight));
        _painter.Repaint(thumbnail, item.Value);

        var frame = new Border
        {
            CornerRadius = new CornerRadius(_options.FrameRadius),
            ClipToBounds = true,
            Child = thumbnail,
        };
        if (_options.FrameBorder) frame.BorderThickness = new Thickness(1);
        frame.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        var tile = new Button
        {
            Width = _options.TileWidth,
            Padding = padding,
            BorderThickness = default,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel
            {
                Spacing = _options.CaptionGap,
                Children =
                {
                    frame,
                    new TextBlock
                    {
                        Text = item.Caption,
                        FontSize = _metrics.FontSizeSmall,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextAlignment = TextAlignment.Center,
                    },
                },
            },
        };
        tile.Classes.Add("ribbon");
        tile.Classes.Add("tile");
        tile.Classes.Set("checked", index == _selected);
        tile.Click += (_, _) => Choose(index, focus: false);
        tile.DoubleTapped += (_, _) =>
        {
            Choose(index, focus: false);
            Invoke();
        };
        ToolTip.SetTip(tile, item.Caption);
        Avalonia.Automation.AutomationProperties.SetName(tile, item.Caption);

        _realized[index] = (tile, thumbnail);
        if (IsVirtualizing)
            tile.DetachedFromVisualTree += (_, _) =>
            {
                if (_realized.TryGetValue(index, out var entry) && ReferenceEquals(entry.Tile, tile)) _realized.Remove(index);
            };
        return tile;
    }
}

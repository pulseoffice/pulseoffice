using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Tabs;

public readonly record struct TabGeometry(double Start, double Padding, double Gap, double FontSize);

public abstract class TabStrip<TItem> : ThemedSurface
{
    private readonly List<(int Index, Rect Bounds, FormattedText Text)> _tabs = [];

    private IReadOnlyList<TItem> _items = [];
    private int _selected;
    private int _hovered = -1;
    private bool _keyboardFocus;

    protected TabStrip(DesignTokens.Metrics metrics)
    {
        Metrics = metrics;
        FocusAdorner = null;
    }

    protected DesignTokens.Metrics Metrics { get; }

    protected bool Reachable
    {
        get;
        init
        {
            field = value;
            Focusable = value;
        }
    }

    protected IReadOnlyList<TItem> Items => _items;

    protected int SelectedIndex => _selected;

    public Rect? BoundsOf(int index)
    {
        if (_tabs.Count == 0) Layout();
        foreach (var tab in _tabs)
            if (tab.Index == index) return tab.Bounds;
        return null;
    }

    protected int HoveredIndex => _hovered;

    protected abstract TabGeometry Geometry { get; }

    protected abstract string Title(TItem item);

    protected virtual bool Visible(TItem item) => true;

    protected abstract void DrawTab(DrawingContext context, TItem item, Rect bounds, FormattedText text, bool selected, bool hovered);

    protected virtual void DrawBackground(DrawingContext context)
    {
    }

    protected abstract void Chosen(int index, TItem item);

    protected virtual void SecondaryPressed(int index, TItem item)
    {
    }

    protected virtual void DoubleTappedOn(int index, TItem item)
    {
    }

    protected void Present(IReadOnlyList<TItem> items, int selected)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
        _selected = selected;
        Announce();
        _hovered = -1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected void Swap(IReadOnlyList<TItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
        Announce();
    }

    private void Announce()
    {
        if (_selected >= 0 && _selected < _items.Count) Avalonia.Automation.AutomationProperties.SetName(this, Title(_items[_selected]));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Layout();
        var width = _tabs.Count == 0 ? 0 : _tabs[^1].Bounds.Right;
        return new Size(Math.Min(width, availableSize.Width), Height);
    }

    private void Layout()
    {
        _tabs.Clear();
        var geometry = Geometry;
        var x = geometry.Start;
        for (var index = 0; index < _items.Count; index++)
        {
            if (!Visible(_items[index])) continue;
            var text = new FormattedText(
                Title(_items[index]),
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, index == _selected ? FontWeight.SemiBold : FontWeight.Normal),
                geometry.FontSize,
                Brushes.Black);
            var width = text.Width + geometry.Padding * 2;
            _tabs.Add((index, new Rect(x, 0, width, Height), text));
            x += width + geometry.Gap;
        }
    }

    public sealed override void Render(DrawingContext context)
    {
        DrawBackground(context);
        foreach (var (index, bounds, text) in _tabs)
            if (index < _items.Count)
            {
                context.FillRectangle(Brushes.Transparent, bounds);
                DrawTab(context, _items[index], bounds, text, index == _selected, index == _hovered);
                if (index == _selected && IsFocused && _keyboardFocus) DrawFocus(context, bounds);
            }
    }

    private void DrawFocus(DrawingContext context, Rect bounds)
    {
        var accent = this.FindResource("PulseAccent") as IBrush ?? Brushes.SteelBlue;
        context.DrawRectangle(null, new Pen(accent, 2), new RoundedRect(bounds.Deflate(new Thickness(1, 3, 1, 1)), Metrics.RadiusTight));
    }

    public bool FocusFromKeyboard()
    {
        if (_tabs.Count == 0) return false;
        Focusable = true;
        return Focus(NavigationMethod.Directional);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _keyboardFocus = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Focusable = Reachable;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _tabs.Count == 0) return;
        var position = _tabs.FindIndex(tab => tab.Index == _selected);
        var target = e.Key switch
        {
            Key.Right => (position + 1) % _tabs.Count,
            Key.Left => (position - 1 + _tabs.Count) % _tabs.Count,
            Key.Home => 0,
            Key.End => _tabs.Count - 1,
            _ => -1,
        };
        if (target < 0) return;
        e.Handled = true;
        _keyboardFocus = true;
        var index = _tabs[target].Index;
        if (index == _selected) return;
        _selected = index;
        Announce();
        InvalidateMeasure();
        InvalidateVisual();
        Chosen(index, _items[index]);
    }

    private int At(Point point)
    {
        foreach (var (index, bounds, _) in _tabs)
            if (bounds.Contains(point)) return index;
        return -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var hovered = At(e.GetPosition(this));
        if (hovered == _hovered) return;
        _hovered = hovered;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = -1;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var index = At(e.GetPosition(this));
        if (index < 0 || index >= _items.Count) return;

        if (index != _selected)
        {
            _selected = index;
            Announce();
            InvalidateMeasure();
            InvalidateVisual();
            Chosen(index, _items[index]);
        }

        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) SecondaryPressed(index, _items[index]);
        e.Handled = true;
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        var index = At(e.GetPosition(this));
        if (index < 0 || index >= _items.Count) return;
        DoubleTappedOn(index, _items[index]);
        e.Handled = true;
    }
}

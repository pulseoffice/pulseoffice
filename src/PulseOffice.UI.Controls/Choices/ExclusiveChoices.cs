using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace PulseOffice.UI.Controls.Choices;

public sealed class ExclusiveChoices
{
    private readonly List<ToggleButton> _items = [];
    private int _selected = -1;

    public event EventHandler<int>? Changed;

    public IReadOnlyList<ToggleButton> Items => _items;

    public int Selected => _selected;

    public void Add(ToggleButton item, string name)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = _items.Count;
        _items.Add(item);
        AutomationProperties.SetName(item, name);
        ToolTip.SetTip(item, name);
        item.IsTabStop = false;
        item.Click += (_, _) => Select(index, notify: true);
        item.KeyDown += OnKey;
    }

    public void Select(int index, bool notify)
    {
        if (index >= _items.Count) index = -1;
        var changed = index != _selected;
        _selected = index;
        for (var position = 0; position < _items.Count; position++)
        {
            _items[position].IsChecked = position == index;
            _items[position].IsTabStop = position == (index < 0 ? 0 : index);
        }

        if (changed && notify) Changed?.Invoke(this, index);
    }

    public int Next(int from, Key key) => key switch
    {
        Key.Left or Key.Up => Math.Max(0, from - 1),
        Key.Right or Key.Down => Math.Min(_items.Count - 1, from + 1),
        Key.Home => 0,
        Key.End => _items.Count - 1,
        _ => from,
    };

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (sender is not ToggleButton item || _items.Count == 0) return;
        var from = _items.IndexOf(item);
        var target = Next(from, e.Key);
        if (e.Key is Key.Space or Key.Enter)
        {
            Select(from, notify: true);
            e.Handled = true;
            return;
        }

        if (target == from && e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)) return;

        Select(target, notify: true);
        _items[target].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }
}

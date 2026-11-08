using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class RibbonGroupView : ContentControl
{
    private const int MinimumVisible = 2;

    private readonly RibbonItemFactory _items;
    private readonly int _rows;
    private readonly Dictionary<(RibbonGroupLevel, int), (Control View, List<RibbonSlot> Slots)> _cache = [];
    private readonly List<RibbonSlot> _popoverSlots = [];

    private RibbonGroup _group;

    public RibbonGroupView(RibbonGroup group, RibbonItemFactory items, int rows)
    {
        _group = group ?? throw new ArgumentNullException(nameof(group));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _rows = rows;

        State = RibbonGroupState.Initial(group.Items.Count, MinimumVisible);
        Show();
    }

    private RibbonBuildContext Context => _items.Context;

    public RibbonGroupState State { get; private set; }

    public string Title => _group.Title;

    public void Apply(RibbonGroupState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state == State) return;

        State = state;
        Show();
        InvalidateMeasure();
    }

    public void Reset() => Apply(RibbonGroupState.Initial(_group.Items.Count, MinimumVisible));

    public void Refresh(RibbonGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        _group = group;

        var items = new Dictionary<string, RibbonItem>(StringComparer.Ordinal);
        foreach (var item in group.Items) items.TryAdd(item.Command.Id.Value, item);

        foreach (var slot in _cache.Values.SelectMany(entry => entry.Slots).Concat(_popoverSlots))
            if (items.TryGetValue(slot.CommandId, out var item))
                slot.Refresh(item);
    }

    private void Show()
    {
        var key = (State.Level, State.Visible);

        if (!_cache.TryGetValue(key, out var entry))
        {
            var slots = new List<RibbonSlot>();
            entry = (Build(slots), slots);
            _cache[key] = entry;
        }

        Content = entry.View;
    }

    private Control Build(List<RibbonSlot> slots) => State.Level switch
    {
        RibbonGroupLevel.Collapsed => BuildCollapsed(),
        RibbonGroupLevel.Partial => Labelled(BuildWithOverflow(slots)),
        RibbonGroupLevel.Compact => Labelled(_items.Build(_group.Items, RibbonForm.Compact, _rows, slots)),
        RibbonGroupLevel.Medium => Labelled(_items.Build(_group.Items, RibbonForm.Medium, _rows, slots)),
        _ => Labelled(_items.Build(_group.Items, RibbonForm.Full, _rows, slots)),
    };

    private Control Labelled(Control items)
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };

        var label = new TextBlock
        {
            Text = _group.Title.ToUpperInvariant(),
            FontSize = Math.Max(9, Context.Metrics.FontSizeSmall - 1),
            LetterSpacing = 0.6,
            Height = _items.GroupLabelHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        Grid.SetRow(items, 0);
        Grid.SetRow(label, 1);
        layout.Children.Add(items);
        layout.Children.Add(label);

        return layout;
    }

    private Control BuildWithOverflow(List<RibbonSlot> slots)
    {
        var shown = _group.Items.Take(State.Visible).ToList();

        var layout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        layout.Children.Add(_items.Build(shown, RibbonForm.Compact, _rows, slots));

        var content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (Context.Chevron(1) is { } chevron)
        {
            chevron.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(chevron);
        }

        content.Children.Add(new TextBlock
        {
            Text = (_group.Items.Count - State.Visible).ToString(System.Globalization.CultureInfo.CurrentCulture),
            FontSize = Math.Max(9, Context.Metrics.FontSizeSmall - 1),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var more = Context.Flat(content, width: 26, height: _rows * _items.ItemHeight);
        more.Padding = default;
        more.VerticalAlignment = VerticalAlignment.Top;

        var flyout = Context.Popover(() => Popover(_group.Items.Skip(State.Visible).ToList()));
        more.Click += (_, _) => Context.Dismissal.Toggle(flyout!, more);
        ToolTip.SetTip(more, _group.Title);
        Avalonia.Automation.AutomationProperties.SetName(more, _group.Title);

        layout.Children.Add(more);
        return layout;
    }

    private Control Popover(IReadOnlyList<RibbonItem> items)
    {
        _popoverSlots.Clear();

        var layout = new StackPanel { Spacing = 2 };
        layout.Children.Add(Context.Caption(_group.Title));
        layout.Children.Add(_items.Build(items, RibbonForm.Popover, _rows, _popoverSlots));
        return layout;
    }

    private Control BuildCollapsed()
    {
        var content = new StackPanel
        {
            Spacing = Context.Metrics.SpacingSmall / 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (_items.GroupIcon(_group, Context.Metrics.IconSize * 1.25) is { } icon)
        {
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(icon);
        }

        var caption = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        caption.Children.Add(new TextBlock
        {
            Text = _group.Title,
            FontSize = Context.Metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
        });

        if (Context.Chevron() is { } chevron)
        {
            chevron.VerticalAlignment = VerticalAlignment.Center;
            caption.Children.Add(chevron);
        }

        content.Children.Add(caption);

        var button = Context.Flat(content, height: _rows * _items.ItemHeight + _items.GroupLabelHeight);
        button.MinWidth = 68;
        button.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);

        var flyout = Context.Popover(() => Popover(_group.Items));
        button.Click += (_, _) => Context.Dismissal.Toggle(flyout!, button);
        ToolTip.SetTip(button, _group.Title);
        Avalonia.Automation.AutomationProperties.SetName(button, _group.Title);

        return button;
    }
}

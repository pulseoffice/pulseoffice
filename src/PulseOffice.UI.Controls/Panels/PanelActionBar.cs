using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Panels;

public sealed record PanelAction(string Key, string Icon, string Tip)
{
    public bool StartsGroup { get; init; }

    public bool Trailing { get; init; }
}

public sealed class PanelActionBar : UserControl
{
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PanelAction> _actions = new(StringComparer.Ordinal);
    private readonly List<StackPanel> _sides = [];

    public PanelActionBar(IIconCatalog icons, DesignTokens.Metrics metrics, IEnumerable<PanelAction> actions)
    {
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(actions);

        var leading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var trailing = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        foreach (var action in actions)
        {
            var target = action.Trailing ? trailing : leading;
            if (action.StartsGroup && target.Children.Count > 0) target.Children.Add(Separator());
            var button = Button(icons, metrics, action);
            _buttons[action.Key] = button;
            _actions[action.Key] = action;
            target.Children.Add(button);
        }

        _sides.Add(leading);
        _sides.Add(trailing);
        UpdateSeparators();

        var row = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(leading, Dock.Left);
        DockPanel.SetDock(trailing, Dock.Right);
        row.Children.Add(leading);
        row.Children.Add(trailing);

        Content = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, metrics.SpacingSmall, 0, 0),
            Child = row,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
    }

    public event EventHandler<string>? Invoked;

    public IReadOnlyList<string> Keys => [.. _buttons.Keys];

    public int SeparatorsShown => _sides.Sum(side => side.Children.OfType<Border>().Count(separator => separator.IsVisible));

    public string Tip(string key) => _actions[key].Tip;

    public bool IsActionEnabled(string key) => _buttons.TryGetValue(key, out var button) && button.IsEnabled && button.IsVisible;

    public bool IsActionVisible(string key) => _buttons.TryGetValue(key, out var button) && button.IsVisible;

    public void SetEnabled(string key, bool enabled)
    {
        if (_buttons.TryGetValue(key, out var button)) button.IsEnabled = enabled;
    }

    public void SetVisible(string key, bool visible)
    {
        if (!_buttons.TryGetValue(key, out var button) || button.IsVisible == visible) return;
        button.IsVisible = visible;
        UpdateSeparators();
    }

    private void UpdateSeparators()
    {
        foreach (var side in _sides)
        {
            var children = side.Children;
            for (var index = 0; index < children.Count; index++)
            {
                if (children[index] is not Border separator) continue;
                var before = children.Take(index).Any(child => child is Button && child.IsVisible);
                var after = children.Skip(index + 1).TakeWhile(child => child is Button).Any(child => child.IsVisible);
                separator.IsVisible = before && after;
            }
        }
    }

    public bool Press(string key)
    {
        if (!IsActionEnabled(key)) return false;
        Invoked?.Invoke(this, key);
        return true;
    }

    private Button Button(IIconCatalog icons, DesignTokens.Metrics metrics, PanelAction action)
    {
        var size = metrics.ControlHeight + 2;
        var button = new Button
        {
            Width = size,
            Height = size,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = icons.Find(action.Icon) is { } geometry
                ? new IconView(geometry, metrics.IconSize) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                : new TextBlock { Text = action.Tip },
        };
        button.Classes.Add("ribbon");
        button.Classes.Add("panelAction");
        ToolTip.SetTip(button, action.Tip);
        ToolTip.SetShowOnDisabled(button, true);
        AutomationProperties.SetName(button, action.Tip);
        button.Click += (_, args) =>
        {
            args.Handled = true;
            Invoked?.Invoke(this, action.Key);
        };
        return button;
    }

    private Border Separator() => new Border
    {
        Width = 1,
        Height = 16,
        Margin = new Thickness(4, 0),
        VerticalAlignment = VerticalAlignment.Center,
    }.Themed(Border.BackgroundProperty, Palette.Keys.Border);
}

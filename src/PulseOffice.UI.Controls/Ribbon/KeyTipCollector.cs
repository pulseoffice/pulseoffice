using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class KeyTipCollector
{
    public IReadOnlyList<KeyTipTarget> Collect(Visual root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var targets = new List<KeyTipTarget>();
        Walk(root, targets);
        return targets;
    }

    private void Walk(Visual parent, List<KeyTipTarget> targets)
    {
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is Control { IsEffectivelyVisible: false }) continue;
            if (child is RibbonSlot slot)
            {
                if (slot.IsEffectivelyEnabled) targets.AddRange(slot.KeyTips());
                continue;
            }
            if (child is Button button)
            {
                if (button.IsEffectivelyEnabled && TitleOf(button) is { Length: > 0 } title)
                    targets.Add(new KeyTipTarget(title, button, new Rect(button.Bounds.Size),
                        () => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
                continue;
            }
            Walk(child, targets);
        }
    }

    public string? TitleOf(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (AutomationProperties.GetName(control) is { Length: > 0 } name) return name;
        if (control is ContentControl { Content: string { Length: > 0 } text }) return text;
        if (control.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).FirstOrDefault(shown => !string.IsNullOrWhiteSpace(shown)) is { } label)
            return label;
        return ToolTip.GetTip(control) as string;
    }
}

public sealed class ButtonKeyTipRegion(Visual root, KeyTipCollector collector) : IKeyTipRegion
{
    public IReadOnlyList<KeyTipTarget> KeyTipTargets() =>
        root is Control { IsEffectivelyVisible: false } ? [] : collector.Collect(root);
}

public sealed class MenuKeyTipRegion(Panel host, KeyTipCollector collector) : IKeyTipRegion
{
    public IReadOnlyList<KeyTipTarget> KeyTipTargets()
    {
        if (!host.IsEffectivelyVisible || !host.IsEffectivelyEnabled) return [];
        var targets = new List<KeyTipTarget>();
        foreach (var menu in host.Children.OfType<Menu>())
            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                if (!item.IsEffectivelyVisible || !item.IsEffectivelyEnabled) continue;
                var title = item.Header is string header ? header.Replace("_", string.Empty, StringComparison.Ordinal) : collector.TitleOf(item);
                if (title is not { Length: > 0 }) continue;
                var opened = item;
                var owner = menu;
                targets.Add(new KeyTipTarget(title, item, new Rect(item.Bounds.Size), () =>
                {
                    owner.Open();
                    opened.IsSubMenuOpen = true;
                    opened.Focus(Avalonia.Input.NavigationMethod.Tab);
                }));
            }
        return targets;
    }
}

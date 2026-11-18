using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Ribbon;

public enum KeyTipLevel { Hidden, Tabs, Commands, Items }

public sealed record KeyTipTarget(string Title, Visual Anchor, Rect Area, Action Activate, bool OpensLevel = false);

public sealed record KeyTipItems(object Owner, IReadOnlyList<KeyTipTarget> Targets);

public interface IKeyTipSource
{
    IReadOnlyList<KeyTipTarget> Tabs();

    IReadOnlyList<KeyTipTarget> Commands();

    KeyTipItems? Items();

    void CloseItems();
}

public interface IKeyTipRegion
{
    IReadOnlyList<KeyTipTarget> KeyTipTargets();
}

public sealed class RibbonKeyTips
{
    private readonly Canvas _layer;
    private readonly IKeyTipSource _source;
    private readonly IKeyTipAssigner _assigner;
    private readonly DesignTokens.Metrics _metrics;
    private readonly List<(string Tip, KeyTipTarget Target, Border Badge, Panel Layer)> _shown = [];
    private string _typed = string.Empty;
    private object? _itemsOwner;
    private bool _stepping;

    public RibbonKeyTips(Canvas layer, IKeyTipSource source, IKeyTipAssigner assigner, DesignTokens.Metrics metrics)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _assigner = assigner ?? throw new ArgumentNullException(nameof(assigner));
        _metrics = metrics;
        _layer.IsHitTestVisible = false;
    }

    public event EventHandler? Opened;

    public event EventHandler? Closed;

    public KeyTipLevel Level { get; private set; }

    public IReadOnlyList<string> Tips => [.. _shown.Where(entry => entry.Badge.IsVisible).Select(entry => entry.Tip)];

    public string? TipOf(string title) => _shown.FirstOrDefault(entry => entry.Target.Title == title).Tip;

    public void Toggle()
    {
        if (Level == KeyTipLevel.Hidden) ShowTabs();
        else Close();
    }

    public void ShowTabs()
    {
        var opening = Level == KeyTipLevel.Hidden;
        Present(KeyTipLevel.Tabs, _source.Tabs());
        if (_shown.Count == 0)
        {
            Close();
            return;
        }
        if (opening) Opened?.Invoke(this, EventArgs.Empty);
    }

    public void ShowCommands()
    {
        Present(KeyTipLevel.Commands, _source.Commands());
        if (_shown.Count == 0) ShowTabs();
    }

    public void StepBack()
    {
        if (_typed.Length > 0)
        {
            Filter(string.Empty);
            return;
        }
        if (Level == KeyTipLevel.Items)
        {
            _stepping = true;
            try
            {
                _source.CloseItems();
            }
            finally
            {
                _stepping = false;
            }
            if (!ShowItems(null)) ShowCommands();
            return;
        }
        if (Level == KeyTipLevel.Commands) ShowTabs();
        else Close();
    }

    public void ItemsClosed()
    {
        if (Level == KeyTipLevel.Items && !_stepping) Close();
    }

    public void Close()
    {
        if (Level == KeyTipLevel.Hidden) return;
        Clear();
        _itemsOwner = null;
        Level = KeyTipLevel.Hidden;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public bool Type(string? symbol)
    {
        if (Level == KeyTipLevel.Hidden) return false;
        if (_assigner.Typed(symbol) is not { } letter) return false;

        var typed = _typed + letter;
        var exact = _shown.FirstOrDefault(entry => entry.Tip == typed);
        if (exact.Target is not null)
        {
            Run(exact.Target);
            return true;
        }

        Filter(_shown.Any(entry => entry.Tip.StartsWith(typed, StringComparison.Ordinal)) ? typed : string.Empty);
        return true;
    }

    private void Run(KeyTipTarget target)
    {
        if (target.OpensLevel && Level == KeyTipLevel.Tabs)
        {
            target.Activate();
            ShowCommands();
            return;
        }

        if (Level == KeyTipLevel.Tabs)
        {
            Close();
            target.Activate();
            return;
        }

        var level = Level;
        var before = level == KeyTipLevel.Items ? _itemsOwner : null;
        target.Activate();
        if (Level == KeyTipLevel.Hidden) return;
        if (ShowItems(before)) return;
        if (level == KeyTipLevel.Items) CloseAllItems();
        Close();
    }

    private bool ShowItems(object? previous)
    {
        if (_source.Items() is not { Targets.Count: > 0 } items || ReferenceEquals(items.Owner, previous)) return false;
        _itemsOwner = items.Owner;
        Present(KeyTipLevel.Items, items.Targets);
        return _shown.Count > 0;
    }

    private void CloseAllItems()
    {
        _stepping = true;
        try
        {
            for (var guard = 0; guard < 8 && _source.Items() is not null; guard++) _source.CloseItems();
        }
        finally
        {
            _stepping = false;
        }
    }

    private void Filter(string typed)
    {
        _typed = typed;
        foreach (var (tip, _, badge, _) in _shown) badge.IsVisible = tip.StartsWith(typed, StringComparison.Ordinal);
    }

    private void Present(KeyTipLevel level, IReadOnlyList<KeyTipTarget> targets)
    {
        Clear();
        Level = level;

        var tips = _assigner.Assign([.. targets.Select(target => target.Title)]);
        for (var index = 0; index < targets.Count; index++)
        {
            if (tips[index].Length == 0) continue;
            var badge = Badge(tips[index], targets[index].Title);
            var layer = Place(badge, targets[index]);
            _shown.Add((tips[index], targets[index], badge, layer));
        }
    }

    private void Clear()
    {
        _typed = string.Empty;
        foreach (var (_, _, badge, layer) in _shown)
            if (!ReferenceEquals(layer, _layer)) layer.Children.Remove(badge);
        _shown.Clear();
        _layer.Children.Clear();
    }

    private Border Badge(string tip, string title)
    {
        var text = new TextBlock
        {
            Text = tip,
            FontSize = _metrics.FontSizeSmall,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.Surface);

        var badge = new Border
        {
            Child = text,
            MinWidth = _metrics.FontSizeSmall + 8,
            Padding = new Thickness(4, 1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            BorderThickness = new Thickness(1),
        }.Themed(Border.BackgroundProperty, Palette.Keys.TextPrimary).Themed(Border.BorderBrushProperty, Palette.Keys.Surface);

        AutomationProperties.SetName(badge, title + " " + tip);
        return badge;
    }

    private Panel Place(Border badge, KeyTipTarget target)
    {
        if (!InRibbon(target.Anchor) && AdornerLayer.GetAdornerLayer(target.Anchor) is { } adorners)
        {
            badge.IsHitTestVisible = false;
            badge.Measure(Size.Infinity);
            badge.HorizontalAlignment = HorizontalAlignment.Center;
            badge.VerticalAlignment = VerticalAlignment.Bottom;
            badge.Margin = new Thickness(0, 0, 0, -badge.DesiredSize.Height / 2);
            AdornerLayer.SetAdornedElement(badge, target.Anchor);
            adorners.Children.Add(badge);
            return adorners;
        }

        _layer.Children.Add(badge);
        PlaceOnLayer(badge, target);
        return _layer;
    }

    private bool InRibbon(Visual anchor) => _layer.GetVisualParent() is { } ribbon && (ReferenceEquals(ribbon, anchor) || ribbon.IsVisualAncestorOf(anchor));

    private void PlaceOnLayer(Border badge, KeyTipTarget target)
    {
        badge.Measure(Size.Infinity);
        var size = badge.DesiredSize;
        var anchor = new Point(target.Area.Center.X, target.Area.Bottom);
        var point = target.Anchor.TranslatePoint(anchor, _layer) ?? anchor;
        var limit = _layer.Bounds.Size;
        var left = Math.Clamp(point.X - size.Width / 2, 0, Math.Max(0, limit.Width - size.Width));
        var top = Math.Clamp(point.Y - size.Height / 2, 0, Math.Max(0, limit.Height - size.Height));
        Canvas.SetLeft(badge, left);
        Canvas.SetTop(badge, top);
    }
}

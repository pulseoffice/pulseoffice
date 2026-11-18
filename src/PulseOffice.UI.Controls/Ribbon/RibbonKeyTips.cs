using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Ribbon;

public enum KeyTipLevel { Hidden, Tabs, Commands }

public sealed record KeyTipTarget(string Title, Visual Anchor, Rect Area, Action Activate, bool OpensLevel = false);

public interface IKeyTipSource
{
    IReadOnlyList<KeyTipTarget> Tabs();

    IReadOnlyList<KeyTipTarget> Commands();
}

public sealed class RibbonKeyTips
{
    private readonly Canvas _layer;
    private readonly IKeyTipSource _source;
    private readonly IKeyTipAssigner _assigner;
    private readonly DesignTokens.Metrics _metrics;
    private readonly List<(string Tip, KeyTipTarget Target, Border Badge)> _shown = [];
    private string _typed = string.Empty;

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
        if (Level == KeyTipLevel.Commands) ShowTabs();
        else Close();
    }

    public void Close()
    {
        if (Level == KeyTipLevel.Hidden) return;
        Clear();
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

        Close();
        target.Activate();
    }

    private void Filter(string typed)
    {
        _typed = typed;
        foreach (var (tip, _, badge) in _shown) badge.IsVisible = tip.StartsWith(typed, StringComparison.Ordinal);
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
            _shown.Add((tips[index], targets[index], badge));
            _layer.Children.Add(badge);
            Place(badge, targets[index]);
        }
    }

    private void Clear()
    {
        _typed = string.Empty;
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

    private void Place(Border badge, KeyTipTarget target)
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

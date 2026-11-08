using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Tabs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class RibbonTabStrip : TabStrip<RibbonTab>
{
    private const double SidePadding = 16;
    private const double Gap = 2;
    private const double AccentBar = 2;

    public RibbonTabStrip(DesignTokens.Metrics metrics) : base(metrics)
    {
        Height = metrics.ControlHeight + metrics.SpacingSmall;
    }

    private Pen? _borderPen;
    private Pen? _seamPen;

    public event EventHandler<RibbonTab>? TabChosen;

    public RibbonTab? Current => SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;

    protected override TabGeometry Geometry => new(SidePadding / 2, SidePadding, Gap, Metrics.FontSize + 1);

    public void Replace(IReadOnlyList<RibbonTab> tabs) => Swap(tabs);

    public void Show(IReadOnlyList<RibbonTab> tabs, string? keepTitle)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        var restored = tabs.ToList().FindIndex(tab => tab.Title == keepTitle);
        Present(tabs, restored >= 0 ? restored : 0);
        if (Current is { } current) TabChosen?.Invoke(this, current);
    }

    protected override string Title(RibbonTab item) => item.Title;

    protected override void DrawBackground(DrawingContext context)
    {
        var baseline = Bounds.Height - 0.5;
        context.DrawLine(BorderPen(), new Point(0, baseline), new Point(Bounds.Width, baseline));
    }

    protected override void DrawTab(DrawingContext context, RibbonTab item, Rect bounds, FormattedText text, bool selected, bool hovered)
    {
        var body = bounds.Deflate(new Thickness(0, 3, 0, 0));
        var radius = new CornerRadius(Metrics.RadiusSoft, Metrics.RadiusSoft, 0, 0);

        if (selected)
        {
            var surface = Themed(Palette.Keys.SurfaceRaised, Brushes.White);
            var baseline = Bounds.Height - 0.5;
            var accent = this.FindResource("PulseAccent") as IBrush ?? Brushes.SteelBlue;
            context.DrawRectangle(surface, BorderPen(), new RoundedRect(body, radius));
            context.DrawLine(SeamPen(surface), new Point(body.X + 1, baseline), new Point(body.Right - 1, baseline));

            var contextual = item is { IsContextual: true, AccentHex: { } hex } && Color.TryParse(hex, out var parsed)
                ? new SolidColorBrush(parsed)
                : null;
            context.FillRectangle(contextual ?? accent, new Rect(body.X + Metrics.RadiusSoft / 2, body.Y, body.Width - Metrics.RadiusSoft, AccentBar));
            text.SetForegroundBrush(contextual ?? this.FindResource("PulseAccentText") as IBrush ?? accent);
        }
        else
        {
            if (hovered) context.DrawRectangle(Themed(Palette.Keys.Hover, Brushes.Transparent), null, new RoundedRect(body, radius));
            text.SetForegroundBrush(Themed(Palette.Keys.TextSecondary, Brushes.Gray));
        }

        context.DrawText(text, new Point(bounds.X + SidePadding, bounds.Y + (bounds.Height - text.Height) / 2 + 1));
    }

    private Pen BorderPen()
    {
        var brush = Themed(Palette.Keys.Border, Brushes.Gray);
        if (_borderPen is null || !ReferenceEquals(_borderPen.Brush, brush)) _borderPen = new Pen(brush, 1);
        return _borderPen;
    }

    private Pen SeamPen(IBrush surface)
    {
        if (_seamPen is null || !ReferenceEquals(_seamPen.Brush, surface)) _seamPen = new Pen(surface, 2);
        return _seamPen;
    }

    protected override void Chosen(int index, RibbonTab item) => TabChosen?.Invoke(this, item);
}

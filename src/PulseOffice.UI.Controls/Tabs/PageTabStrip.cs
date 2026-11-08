using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Tabs;

public sealed class PageTabStrip : TabStrip<string>
{
    private const double SidePadding = 12;
    private const double Gap = 2;
    private const double Indicator = 2.5;
    private const double IndicatorInset = 6;

    public PageTabStrip(DesignTokens.Metrics metrics, IReadOnlyList<string>? titles = null, int selected = 0) : base(metrics)
    {
        Height = metrics.ControlHeight + metrics.SpacingSmall + 2;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Reachable = true;
        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        if (titles is not null) Show(titles, selected);
    }

    public event EventHandler<int>? SelectionChanged;

    public int Selected => SelectedIndex;

    public IReadOnlyList<string> Titles => Items;

    public void Show(IReadOnlyList<string> titles, int selected)
    {
        ArgumentNullException.ThrowIfNull(titles);
        Present(titles, Math.Clamp(selected, 0, Math.Max(0, titles.Count - 1)));
        Avalonia.Automation.AutomationProperties.SetName(this, titles.Count > selected && selected >= 0 ? titles[selected] : string.Empty);
    }

    public void Select(int index)
    {
        if (index < 0 || index >= Items.Count || index == SelectedIndex) return;
        Present(Items, index);
        Chosen(index, Items[index]);
    }

    protected override TabGeometry Geometry => new(0, SidePadding, Gap, Metrics.FontSize + 0.5);

    protected override string Title(string item) => item;

    protected override void DrawBackground(DrawingContext context)
    {
        var baseline = Bounds.Height - 0.5;
        context.DrawLine(new Pen(Themed(Palette.Keys.Border, Brushes.Gray), 1), new Point(0, baseline), new Point(Bounds.Width, baseline));
    }

    protected override void DrawTab(DrawingContext context, string item, Rect bounds, FormattedText text, bool selected, bool hovered)
    {
        var accent = this.FindResource("PulseAccent") as IBrush ?? Brushes.SteelBlue;
        if (hovered && !selected)
        {
            var body = bounds.Deflate(new Thickness(1, 3, 1, Indicator + 2));
            context.DrawRectangle(Themed(Palette.Keys.Hover, Brushes.Transparent), null, new RoundedRect(body, Metrics.RadiusTight));
        }

        if (selected)
        {
            var bar = new Rect(bounds.X + IndicatorInset, bounds.Bottom - Indicator, Math.Max(4, bounds.Width - IndicatorInset * 2), Indicator);
            context.DrawRectangle(accent, null, new RoundedRect(bar, Indicator / 2, Indicator / 2, 0, 0));
            text.SetForegroundBrush(this.FindResource("PulseAccentText") as IBrush ?? accent);
        }
        else
            text.SetForegroundBrush(Themed(hovered ? Palette.Keys.TextPrimary : Palette.Keys.TextSecondary, Brushes.Gray));

        context.DrawText(text, new Point(bounds.X + SidePadding, bounds.Y + (bounds.Height - Indicator - text.Height) / 2 + 1));
    }

    protected override void Chosen(int index, string item)
    {
        Avalonia.Automation.AutomationProperties.SetName(this, item);
        SelectionChanged?.Invoke(this, index);
    }
}

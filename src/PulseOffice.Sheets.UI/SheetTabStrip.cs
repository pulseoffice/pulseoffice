using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.UI.Controls.Tabs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI;

public sealed class SheetTabStrip : TabStrip<Worksheet>
{
    private readonly double _colourBar;

    public SheetTabStrip(DesignTokens.Metrics metrics) : base(metrics)
    {
        _colourBar = metrics.SpacingSmall - 1;
        Height = metrics.ControlHeight;
        ClipToBounds = true;
    }

    public event EventHandler<int>? SheetChosen;

    public event EventHandler<int>? RenameRequested;

    public event EventHandler<int>? MenuRequested;

    public int Selected => SelectedIndex;

    protected override TabGeometry Geometry =>
        new(0, Metrics.SpacingLarge - Metrics.SpacingSmall, Metrics.SpacingSmall / 2, Metrics.FontSize);

    public void Show(IReadOnlyList<Worksheet> sheets, int selected) => Present(sheets, selected);

    protected override string Title(Worksheet item) => item.Name;

    protected override bool Visible(Worksheet item) => !item.IsHidden;

    protected override void DrawTab(DrawingContext context, Worksheet item, Rect bounds, FormattedText text, bool selected, bool hovered)
    {
        var shape = new RoundedRect(bounds.Deflate(new Thickness(0, 2, 0, 0)), new CornerRadius(Metrics.Radius, Metrics.Radius, 0, 0));

        if (selected) context.DrawRectangle(Themed(Palette.Keys.Surface, Brushes.White), new Pen(Themed(Palette.Keys.Border, Brushes.Gray), 1), shape);
        else if (hovered) context.DrawRectangle(Themed(Palette.Keys.Hover, Brushes.Transparent), null, shape);

        if (item.TabColorHex is { } hex && Color.TryParse(hex, out var colour))
            context.FillRectangle(
                new SolidColorBrush(colour),
                new Rect(bounds.X + _colourBar, bounds.Bottom - _colourBar, bounds.Width - _colourBar * 2, _colourBar));

        text.SetForegroundBrush(selected
            ? this.FindResource("PulseAccentText") as IBrush ?? Brushes.SteelBlue
            : Themed(Palette.Keys.TextSecondary, Brushes.Gray));
        context.DrawText(text, new Point(bounds.X + Geometry.Padding, bounds.Y + (bounds.Height - text.Height) / 2));
    }

    protected override void Chosen(int index, Worksheet item) => SheetChosen?.Invoke(this, index);

    protected override void SecondaryPressed(int index, Worksheet item) => MenuRequested?.Invoke(this, index);

    protected override void DoubleTappedOn(int index, Worksheet item) => RenameRequested?.Invoke(this, index);
}

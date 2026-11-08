using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Design;

public sealed record FooterPlaceholderBox(string Kind, double Left, double Top, double Width, double Height, bool Active);

public sealed class FooterPlaceholderOverlay : Canvas
{
    public FooterPlaceholderOverlay(double width, double height, bool date, bool footer, bool number)
    {
        Width = width;
        Height = height;
        IsHitTestVisible = false;
        Boxes =
        [
            new("date", 0.04, 0.925, 0.24, 0.053, date),
            new("footer", 0.36, 0.925, 0.28, 0.053, footer),
            new("number", 0.72, 0.925, 0.24, 0.053, number),
        ];
        foreach (var box in Boxes)
        {
            var mark = new Border
            {
                Width = box.Width * width,
                Height = Math.Max(6, box.Height * height),
                BorderThickness = new Thickness(box.Active ? 2 : 1),
                CornerRadius = new CornerRadius(2),
                Opacity = box.Active ? 1 : 0.6,
                Background = Brushes.Transparent,
            }.Themed(Border.BorderBrushProperty, box.Active ? "PulseAccent" : Palette.Keys.BorderStrong);
            if (box.Active) mark.Themed(Border.BackgroundProperty, "PulseAccentSubtle");
            SetLeft(mark, box.Left * width);
            SetTop(mark, Math.Min(height - mark.Height, box.Top * height));
            Children.Add(mark);
        }
    }

    public IReadOnlyList<FooterPlaceholderBox> Boxes { get; }
}

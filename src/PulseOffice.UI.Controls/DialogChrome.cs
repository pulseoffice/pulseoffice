using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public class DialogChrome(IIconCatalog icons, DesignTokens.Metrics metrics)
{
    protected IIconCatalog Icons { get; } = icons ?? throw new ArgumentNullException(nameof(icons));

    public DesignTokens.Metrics Metrics { get; } = metrics;

    public TextBlock Caption(string text) => new TextBlock
    {
        Text = text.ToUpper(CultureInfo.CurrentCulture),
        FontSize = Math.Max(9, Metrics.FontSizeSmall - 1),
        FontWeight = FontWeight.Medium,
        LetterSpacing = 0.6,
        VerticalAlignment = VerticalAlignment.Center,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    public TextBlock Caption(string text, Thickness margin)
    {
        var caption = Caption(text);
        caption.Margin = margin;
        return caption;
    }

    public TextBlock Description(string text) => new TextBlock
    {
        Text = text,
        FontSize = Metrics.FontSizeSmall,
        TextWrapping = TextWrapping.Wrap,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    public Control Icon(string key, double size) =>
        Icons.Find(key) is { } geometry
            ? new IconView(geometry, size) { VerticalAlignment = VerticalAlignment.Center }
                .Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary)
            : new Border { Width = size, Height = size };

    public Border Dot()
    {
        var dot = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, Metrics.SpacingSmall, 0),
        };
        dot.Bind(Border.BackgroundProperty, dot.GetResourceObservable("PulseAccent"));
        return dot;
    }

    public Border Card(Control child) => new Border
    {
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(Metrics.RadiusSoft),
        ClipToBounds = true,
        Child = child,
    }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
     .Themed(Border.BackgroundProperty, Palette.Keys.Surface);
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Fonts;

public sealed class LicenseBadge : Border
{
    public LicenseBadge(string text, string colourKey, DesignTokens.Metrics metrics, string? tip = null)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = metrics.FontSizeSmall,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, colourKey);

        Child = label;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(999);
        Padding = new Thickness(metrics.SpacingSmall + 2, 1);
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Left;
        this.Themed(BorderBrushProperty, colourKey);
        if (tip is not null) ToolTip.SetTip(this, tip);
    }
}

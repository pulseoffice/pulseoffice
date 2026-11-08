using Avalonia;
using Avalonia.Controls;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Panels;

public sealed class PanelListFrame : Border
{
    public PanelListFrame(DesignTokens.Metrics metrics, Control list, Control? empty = null)
    {
        ArgumentNullException.ThrowIfNull(list);
        list.Classes.Add("panelList");
        var stack = new Panel();
        stack.Children.Add(list);
        if (empty is not null) stack.Children.Add(empty);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(metrics.RadiusTight + 2);
        ClipToBounds = true;
        Child = stack;
        this.Themed(BorderBrushProperty, Palette.Keys.Border).Themed(BackgroundProperty, Palette.Keys.Surface);
    }
}

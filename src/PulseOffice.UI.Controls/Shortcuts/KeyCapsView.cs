using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.UI.Commands.Shortcuts;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Shortcuts;

public sealed class KeyCapsView : StackPanel
{
    public KeyCapsView(string? gesture, IGestureNotation notation, DesignTokens.Metrics metrics)
    {
        ArgumentNullException.ThrowIfNull(notation);

        Orientation = Orientation.Horizontal;
        Spacing = 3;
        VerticalAlignment = VerticalAlignment.Center;

        Keys = notation.Keys(gesture);

        foreach (var key in Keys)
        {
            var cap = new Border
            {
                MinWidth = 18,
                Padding = new Thickness(5, 0),
                Height = 18,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1, 1, 1, 2),
                Child = new TextBlock
                {
                    Text = key,
                    FontSize = metrics.FontSize - 2,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
            };
            cap.Classes.Add("keycap");
            cap.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised)
                .Themed(Border.BorderBrushProperty, Palette.Keys.BorderStrong);
            Children.Add(cap);
        }

        AutomationProperties.SetName(this, notation.Display(gesture));
    }

    public IReadOnlyList<string> Keys { get; }
}

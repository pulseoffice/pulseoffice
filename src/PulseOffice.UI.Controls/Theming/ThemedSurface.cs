using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace PulseOffice.UI.Controls.Theming;

public abstract class ThemedSurface : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ThemeVariantScope.ActualThemeVariantProperty) InvalidateVisual();
    }

    protected IBrush Themed(string key, IBrush fallback) =>
        this.FindResource(key) as IBrush ?? fallback;
}

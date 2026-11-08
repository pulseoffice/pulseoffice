using Avalonia.Controls;
using Avalonia.Media;

namespace PulseOffice.UI.Controls.Theming;

public sealed class ResourceBrushes
{
    public IBrush Find(Control owner, string key, IBrush fallback)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!owner.TryFindResource(key, owner.ActualThemeVariant, out var value)) return fallback;
        return value switch
        {
            IBrush brush => brush,
            Color colour => new SolidColorBrush(colour),
            _ => fallback,
        };
    }
}

using Avalonia;
using Avalonia.Controls;

namespace PulseOffice.UI.Controls.Theming;

public static class ThemedElement
{
    public static T Themed<T>(this T control, AvaloniaProperty property, string resourceKey)
    where T : Control
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(property);

        control.Bind(property, control.GetResourceObservable(resourceKey));
        return control;
    }

    public static T ThemedInline<T>(this T inline, AvaloniaProperty property, string resourceKey)
    where T : Avalonia.Controls.Documents.Inline
    {
        ArgumentNullException.ThrowIfNull(inline);
        ArgumentNullException.ThrowIfNull(property);

        inline.Bind(property, inline.GetResourceObservable(resourceKey));
        return inline;
    }
}

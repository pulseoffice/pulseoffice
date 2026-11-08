using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace PulseOffice.UI.Controls.Theming;

public sealed class PulseTheme : Styles
{
    public PulseTheme() => AvaloniaXamlLoader.Load(this);
}

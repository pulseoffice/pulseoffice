using PulseOffice.Platform;

namespace PulseOffice.Platform.Windows;

public sealed class WindowsWindowChrome : IWindowChrome
{
    public MenuPlacement MenuPlacement => MenuPlacement.InTitleBar;

    public double LeadingInset => 8;

    public double TrailingInset => 0;

    public bool ShowsTitleText => true;

    public bool DrawsCaptionButtons => true;
}

using PulseOffice.Platform;

namespace PulseOffice.Platform.MacOS;

public sealed class MacWindowChrome : IWindowChrome
{
    public MenuPlacement MenuPlacement => MenuPlacement.SystemMenuBar;

    public double LeadingInset => 78;

    public double TrailingInset => 8;

    public bool ShowsTitleText => true;

    public bool DrawsCaptionButtons => false;

    public bool DrawsWindowShadow => false;
}

using PulseOffice.Platform;
using PulseOffice.Platform.Linux;
using PulseOffice.Platform.MacOS;
using PulseOffice.Platform.Windows;

namespace PulseOffice.Desktop.Composition;

public sealed class WindowChromeProvider(IHostSystem host) : IWindowChromeProvider
{
    private readonly Lazy<IWindowChrome> _chrome = new(() => Select(host));

    public IWindowChrome Current => _chrome.Value;

    private static IWindowChrome Select(IHostSystem host)
    {
        if (host.IsMacOS) return new MacWindowChrome();
        if (host.IsWindows) return new WindowsWindowChrome();

        return new LinuxWindowChrome();
    }
}

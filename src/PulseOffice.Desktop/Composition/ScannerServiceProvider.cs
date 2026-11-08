using PulseOffice.Platform;
using PulseOffice.Platform.Windows;

namespace PulseOffice.Desktop.Composition;

public sealed class ScannerServiceProvider(IExecutableLocator executables, IHostSystem host) : IScannerServiceProvider
{
    private readonly Lazy<IScannerService> _scanner = new(() => Select(executables, host));

    public IScannerService Current => _scanner.Value;

    private static IScannerService Select(IExecutableLocator executables, IHostSystem host)
    {
        if (host.IsWindows)
        {
            var wia = new WiaScannerService();
            return wia.IsAvailable ? wia : new UnavailableScannerService("WIA");
        }

        var sane = new SaneScannerService(executables);

        if (sane.IsAvailable) return sane;

        return new UnavailableScannerService("scanimage (SANE)");
    }
}

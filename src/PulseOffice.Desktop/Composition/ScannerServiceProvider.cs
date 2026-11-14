using PulseOffice.Platform;
using PulseOffice.Platform.Windows;

namespace PulseOffice.Desktop.Composition;

public sealed class ScannerServiceProvider(IExecutableLocator executables, IHostSystem host, IExternalProcess processes) : IScannerServiceProvider
{
    private IScannerService? _scanner;

    public IScannerService Current => _scanner ??= Select();

    private IScannerService Select()
    {
        if (host.IsWindows)
        {
            var wia = new WiaScannerService(processes);
            return wia.IsAvailable ? wia : new UnavailableScannerService("WIA");
        }

        var sane = new SaneScannerService(executables, processes);

        if (sane.IsAvailable) return sane;

        return new UnavailableScannerService("scanimage (SANE)");
    }
}

using PulseOffice.Platform;
using PulseOffice.Platform.Linux;
using PulseOffice.Platform.MacOS;
using PulseOffice.Platform.Windows;

namespace PulseOffice.Desktop.Composition;

public sealed class UpdateInstallerProvider(IHostSystem host, IExternalProcess processes, IAppImageLocation appImage)
{
    public IUpdateInstaller Select() =>
        host.IsMacOS ? new MacUpdateInstaller(processes)
        : host.IsWindows ? new WindowsUpdateInstaller()
        : host.IsLinux ? new AppImageUpdateInstaller(appImage)
        : new ManualUpdateInstaller();
}

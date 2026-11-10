using PulseOffice.Platform;
using PulseOffice.Platform.Linux;

namespace PulseOffice.Desktop.Composition;

public sealed class DesktopIntegrationProvider(
    IHostSystem host,
    IAppImageLocation location,
    IEnvironmentVariables environment,
    IExternalProcess processes,
    IAppImageLauncherScript launcher)
{
    public IDesktopIntegration Select() =>
        host.IsLinux ? new AppImageDesktopIntegration(location, environment, processes, launcher) : new UnavailableDesktopIntegration();
}

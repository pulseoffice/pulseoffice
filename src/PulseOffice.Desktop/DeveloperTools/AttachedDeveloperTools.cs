using Avalonia;

namespace PulseOffice.Desktop.DeveloperTools;

internal sealed class ConfiguredDeveloperTools : IDeveloperTools
{
    public void Attach(Application application) => application.AttachDeveloperTools();
}

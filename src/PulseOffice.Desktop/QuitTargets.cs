using Avalonia.Controls.ApplicationLifetimes;
using PulseOffice.Shell;

namespace PulseOffice.Desktop;

internal sealed class LifetimeQuitTarget(IClassicDesktopStyleApplicationLifetime lifetime) : IQuitTarget
{
    public void Quit() => lifetime.Shutdown();
}

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PulseOffice.Shell;

namespace PulseOffice.Desktop;

internal sealed class WindowQuitTarget(Window window) : IQuitTarget
{
    public void Quit() => window.Close();
}

internal sealed class LifetimeQuitTarget(IClassicDesktopStyleApplicationLifetime lifetime) : IQuitTarget
{
    public void Quit() => lifetime.Shutdown();
}

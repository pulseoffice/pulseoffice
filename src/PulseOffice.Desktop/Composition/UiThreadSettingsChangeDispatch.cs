using Avalonia.Threading;
using PulseOffice.Settings;

namespace PulseOffice.Desktop.Composition;

internal sealed class UiThreadSettingsChangeDispatch : ISettingsChangeDispatch
{
    public void Post(Action notify)
    {
        if (Dispatcher.UIThread.CheckAccess()) notify();
        else Dispatcher.UIThread.Post(notify);
    }
}

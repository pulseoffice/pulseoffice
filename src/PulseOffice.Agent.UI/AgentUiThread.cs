using Avalonia.Threading;

namespace PulseOffice.Agent.UI;

public interface IAgentUiThread
{
    Task<T> InvokeAsync<T>(Func<Task<T>> work);
}

public sealed class AvaloniaAgentUiThread : IAgentUiThread
{
    public Task<T> InvokeAsync<T>(Func<Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return Dispatcher.UIThread.CheckAccess() ? work() : Dispatcher.UIThread.InvokeAsync(work);
    }
}

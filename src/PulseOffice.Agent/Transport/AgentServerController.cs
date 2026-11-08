using Microsoft.Extensions.Logging;
using PulseOffice.Agent.Security;

namespace PulseOffice.Agent.Transport;

public interface IAgentServerController
{
    Task Settled { get; }

    void Start();
}

public sealed class AgentServerController(
    IAgentPreferences preferences,
    IAgentServer server,
    ILogger<AgentServerController> logger) : IAgentServerController
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _state = new();
    private bool _started;
    private int _port;
    private Task _settled = Task.CompletedTask;

    public Task Settled
    {
        get
        {
            lock (_state) return _settled;
        }
    }

    public void Start()
    {
        lock (_state)
        {
            if (_started) return;
            _started = true;
        }

        preferences.Changed += (_, _) => Apply();
        Apply();
    }

    private void Apply()
    {
        var applying = ApplyAsync();
        lock (_state) _settled = applying;
    }

    private async Task ApplyAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!preferences.Enabled)
            {
                if (server.IsRunning) logger.LogInformation("Agent access switched off");
                await server.StopAsync().ConfigureAwait(false);
                return;
            }

            if (server.IsRunning && _port == preferences.Port) return;
            await server.StopAsync().ConfigureAwait(false);
            _port = preferences.Port;
            server.Start(_port);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.LogError(ex, "The agent preferences could not be applied");
        }
        finally
        {
            _gate.Release();
        }
    }
}

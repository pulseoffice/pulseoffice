using System.Net;

namespace PulseOffice.Presentations.Remote;

public interface IRemoteAttemptThrottle
{
    TimeSpan Wait(IPAddress client);

    void Failed(IPAddress client);

    void Succeeded(IPAddress client);

    void Reset();
}

public sealed class RemoteAttemptThrottle(TimeProvider clock) : IRemoteAttemptThrottle
{
    private const int FreeAttempts = 3;
    private const int MaxTracked = 4096;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    private readonly Lock _gate = new();
    private readonly Dictionary<IPAddress, (int Failures, DateTimeOffset Until)> _clients = [];

    public TimeSpan Wait(IPAddress client)
    {
        lock (_gate)
        {
            if (!_clients.TryGetValue(Key(client), out var entry)) return TimeSpan.Zero;
            var left = entry.Until - clock.GetUtcNow();
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    public void Failed(IPAddress client)
    {
        lock (_gate)
        {
            var key = Key(client);
            var failures = (_clients.TryGetValue(key, out var entry) ? entry.Failures : 0) + 1;
            if (!_clients.ContainsKey(key) && _clients.Count >= MaxTracked) Prune();
            _clients[key] = (failures, clock.GetUtcNow() + Delay(failures));
        }
    }

    public void Succeeded(IPAddress client)
    {
        lock (_gate) _clients.Remove(Key(client));
    }

    public void Reset()
    {
        lock (_gate) _clients.Clear();
    }

    private static TimeSpan Delay(int failures)
    {
        if (failures < FreeAttempts) return TimeSpan.Zero;
        var exponent = Math.Min(failures - FreeAttempts, 16);
        var seconds = Math.Min(Math.Pow(2, exponent), MaxDelay.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    private static IPAddress Key(IPAddress client) => client.IsIPv4MappedToIPv6 ? client.MapToIPv4() : client;

    private void Prune()
    {
        var now = clock.GetUtcNow();
        foreach (var expired in _clients.Where(pair => pair.Value.Until + MaxDelay <= now).Select(pair => pair.Key).ToList())
            _clients.Remove(expired);
        if (_clients.Count < MaxTracked) return;
        foreach (var oldest in _clients.OrderBy(pair => pair.Value.Until).Take(_clients.Count / 4).Select(pair => pair.Key).ToList())
            _clients.Remove(oldest);
    }
}

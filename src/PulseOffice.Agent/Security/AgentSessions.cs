using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace PulseOffice.Agent.Security;

public sealed record AgentSession(string Id, string Client, string ProtocolVersion, DateTimeOffset Started)
{
    public DateTimeOffset LastSeen { get; set; } = Started;
}

public sealed record AgentLimits(int MaximumSessions, int MaximumRequestsPerSession, int MaximumRequests, TimeSpan IdleExpiry)
{
    public AgentLimits()
        : this(16, 4, 16, TimeSpan.FromMinutes(10))
    {
    }
}

public interface IAgentSessions
{
    AgentSession? TryCreate(string client, string protocolVersion);

    AgentSession? Find(string? id);

    void Touch(AgentSession session);

    bool End(string id);

    void Clear();

    IReadOnlyList<AgentSession> Active { get; }

    event EventHandler? Changed;
}

public sealed class AgentSessions(TimeProvider time, AgentLimits limits) : IAgentSessions
{
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new(StringComparer.Ordinal);
    private readonly Lock _creation = new();

    public event EventHandler? Changed;

    public IReadOnlyList<AgentSession> Active
    {
        get
        {
            Expire();
            return [.. _sessions.Values.OrderBy(session => session.Started)];
        }
    }

    public AgentSession? TryCreate(string client, string protocolVersion)
    {
        Expire();
        AgentSession session;
        lock (_creation)
        {
            if (_sessions.Count >= limits.MaximumSessions) return null;
            session = new AgentSession(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)), client, protocolVersion, time.GetUtcNow());
            _sessions[session.Id] = session;
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return session;
    }

    public AgentSession? Find(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Expire();
        return _sessions.TryGetValue(id, out var session) ? session : null;
    }

    public void Touch(AgentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.LastSeen = time.GetUtcNow();
    }

    public bool End(string id)
    {
        if (!_sessions.TryRemove(id, out _)) return false;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        if (_sessions.IsEmpty) return;
        _sessions.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Expire()
    {
        var now = time.GetUtcNow();
        var expired = _sessions.Values.Where(session => now - session.LastSeen > limits.IdleExpiry).Select(session => session.Id).ToList();
        if (expired.Count == 0) return;
        foreach (var id in expired) _sessions.TryRemove(id, out _);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public interface IAgentRequestGate
{
    IDisposable? TryEnter(AgentSession session);
}

public sealed class AgentRequestGate(AgentLimits limits) : IAgentRequestGate
{
    private readonly Dictionary<string, int> _perSession = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private int _total;

    public IDisposable? TryEnter(AgentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (_lock)
        {
            var running = _perSession.GetValueOrDefault(session.Id);
            if (_total >= limits.MaximumRequests || running >= limits.MaximumRequestsPerSession) return null;
            _perSession[session.Id] = running + 1;
            _total++;
        }
        return new Ticket(this, session.Id);
    }

    private void Leave(string id)
    {
        lock (_lock)
        {
            _total--;
            if (--_perSession[id] == 0) _perSession.Remove(id);
        }
    }

    private sealed class Ticket(AgentRequestGate gate, string id) : IDisposable
    {
        private int _left;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _left, 1) == 0) gate.Leave(id);
        }
    }
}

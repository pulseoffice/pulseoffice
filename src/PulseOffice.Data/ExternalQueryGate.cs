using System.Runtime.CompilerServices;

namespace PulseOffice.Data;

public sealed record ExternalQueryRequest(string ConnectionId, string Sql);

public interface IExternalQueryGate
{
    Task<bool> AllowAsync(object document, IReadOnlyList<ExternalQueryRequest> requests, CancellationToken ct = default);

    void Grant(object document, ExternalQueryRequest request);
}

public interface IExternalQueryConsentLedger
{
    IReadOnlyList<ExternalQueryRequest> Pending(object document, IEnumerable<ExternalQueryRequest> requests);

    void Grant(object document, IEnumerable<ExternalQueryRequest> requests);
}

public sealed class ExternalQueryConsentLedger : IExternalQueryConsentLedger
{
    private readonly ConditionalWeakTable<object, HashSet<ExternalQueryRequest>> _granted = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<ExternalQueryRequest> Pending(object document, IEnumerable<ExternalQueryRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(requests);
        lock (_gate)
        {
            var granted = _granted.TryGetValue(document, out var set) ? set : null;
            return [.. requests.Distinct().Where(request => granted?.Contains(request) != true)];
        }
    }

    public void Grant(object document, IEnumerable<ExternalQueryRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(requests);
        lock (_gate)
            _granted.GetOrCreateValue(document).UnionWith(requests);
    }
}

using PulseOffice.Data;

namespace PulseOffice.Sheets.Queries.Tests;

internal sealed class RecordingQueryGate(bool allow) : IExternalQueryGate
{
    public List<(object Document, IReadOnlyList<ExternalQueryRequest> Requests)> Asked { get; } = [];

    public Task<bool> AllowAsync(object document, IReadOnlyList<ExternalQueryRequest> requests, CancellationToken ct = default)
    {
        Asked.Add((document, requests));
        return Task.FromResult(allow);
    }

    public void Grant(object document, ExternalQueryRequest request)
    {
    }
}

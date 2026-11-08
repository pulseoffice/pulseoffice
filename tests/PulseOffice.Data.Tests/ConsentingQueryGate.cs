using PulseOffice.Data;

namespace PulseOffice.Data.Tests;

internal sealed class ConsentingQueryGate : IExternalQueryGate
{
    public Task<bool> AllowAsync(object document, IReadOnlyList<ExternalQueryRequest> requests, CancellationToken ct = default) =>
        Task.FromResult(true);

    public void Grant(object document, ExternalQueryRequest request)
    {
    }
}

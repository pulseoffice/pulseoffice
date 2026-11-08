namespace PulseOffice.Exchange;

public interface IExchangeClipboard
{
    bool IsAvailable { get; }

    Task WriteAsync(IReadOnlyList<ExchangeRepresentation> representations, CancellationToken ct = default);

    Task<IReadOnlyList<ExchangeRepresentation>> ReadAsync(CancellationToken ct = default);
}

public sealed class MemoryExchangeClipboard : IExchangeClipboard
{
    private IReadOnlyList<ExchangeRepresentation> _content = [];

    public bool IsAvailable => true;

    public Task WriteAsync(IReadOnlyList<ExchangeRepresentation> representations, CancellationToken ct = default)
    {
        _content = [.. representations];
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ExchangeRepresentation>> ReadAsync(CancellationToken ct = default) => Task.FromResult(_content);
}

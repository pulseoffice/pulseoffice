using Avalonia.Headless;

namespace PulseOffice.Testing;

public sealed class UiThreadSession
{
    private readonly HeadlessUnitTestSession _session;

    private UiThreadSession(HeadlessUnitTestSession session) => _session = session;

    public static UiThreadSession StartNew(Type entryPointType) => new(HeadlessUnitTestSession.StartNew(entryPointType));

    public Task Dispatch(Action action, CancellationToken cancellationToken) =>
        _session.Dispatch(action, cancellationToken);

    public Task<TResult> Dispatch<TResult>(Func<TResult> action, CancellationToken cancellationToken) =>
        _session.Dispatch(action, cancellationToken);

    public async Task Dispatch(Func<Task> action, CancellationToken cancellationToken) =>
        await await _session.Dispatch(action, cancellationToken);

    public Task<TResult> Dispatch<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken) =>
        _session.Dispatch(action, cancellationToken);
}

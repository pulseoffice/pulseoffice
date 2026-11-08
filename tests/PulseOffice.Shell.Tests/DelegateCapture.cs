using PulseOffice.Shell;

namespace PulseOffice.Shell.Tests;

internal sealed class DelegateCapture(object? model, Func<object?, Stream, CancellationToken, Task> write) : IRecoveryCapture
{
    public Task WriteAsync(Stream destination, CancellationToken ct = default) => write(model, destination, ct);
}

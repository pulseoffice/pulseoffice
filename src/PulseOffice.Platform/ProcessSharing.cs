using System.Diagnostics;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Platform;

public interface ISharingProcessRunner
{
    Task<OperationResult<bool>> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken ct);
}

public sealed class SharingProcessRunner(IExternalProcess processes) : ISharingProcessRunner
{
    public async Task<OperationResult<bool>> RunAsync(
        string executable, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        try
        {
            var outcome = await processes.RunAsync(executable, arguments, ct: ct).ConfigureAwait(false);
            if (outcome is null) return OperationResult<bool>.Fail(Diagnostic.Error("SHARE002", executable).With(executable));

            return outcome.Succeeded
                ? OperationResult<bool>.Ok(true)
                : OperationResult<bool>.Fail(Diagnostic.Error("SHARE001", outcome.Error.Trim()).With(outcome.Error.Trim()));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("SHARE002", ex.Message).With(ex.Message));
        }
    }
}

using System.Diagnostics;

namespace PulseOffice.Platform;

public sealed record ProcessOutcome(int ExitCode, string Output, string Error)
{
    public bool Succeeded => ExitCode == 0;

    public string Explanation(string command) =>
        Error.Trim() is { Length: > 0 } error ? error : $"{command} exited with {ExitCode}";
}

public interface IExternalProcess
{
    Task<ProcessOutcome?> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        string? input = null,
        CancellationToken ct = default);
}

public sealed class ExternalProcess : IExternalProcess
{
    public async Task<ProcessOutcome?> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        string? input = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        ct.ThrowIfCancellationRequested();

        var info = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = input is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (environment is not null)
            foreach (var (name, value) in environment) info.Environment[name] = value;

        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        using var process = Process.Start(info);
        if (process is null) return null;

        try
        {
            var output = process.StandardOutput.ReadToEndAsync(ct);
            var error = process.StandardError.ReadToEndAsync(ct);

            if (input is not null)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), ct).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            await Task.WhenAll(output, error).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            return new ProcessOutcome(process.ExitCode, output.Result, error.Result);
        }
        catch (OperationCanceledException)
        {
            Stop(process);
            throw;
        }
    }

    private void Stop(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
        }
    }
}

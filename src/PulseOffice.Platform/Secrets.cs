namespace PulseOffice.Platform;

public interface ISecretVault
{
    string DisplayName { get; }

    bool IsAvailable { get; }

    Task<string?> ReadAsync(string service, string account, CancellationToken ct = default);

    Task<bool> WriteAsync(string service, string account, string secret, CancellationToken ct = default);

    Task<bool> RemoveAsync(string service, string account, CancellationToken ct = default);
}

public interface ISecretVaultProvider
{
    ISecretVault Current { get; }
}

public sealed class SessionSecretVault : ISecretVault
{
    private readonly Dictionary<string, string> _secrets = [];
    private readonly Lock _gate = new();

    public string DisplayName => "Session";

    public bool IsAvailable => false;

    public Task<string?> ReadAsync(string service, string account, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_secrets.GetValueOrDefault(Key(service, account)));
    }

    public Task<bool> WriteAsync(string service, string account, string secret, CancellationToken ct = default)
    {
        lock (_gate) _secrets[Key(service, account)] = secret;
        return Task.FromResult(true);
    }

    public Task<bool> RemoveAsync(string service, string account, CancellationToken ct = default)
    {
        lock (_gate) _secrets.Remove(Key(service, account));
        return Task.FromResult(true);
    }

    private static string Key(string service, string account) => service + "\u0000" + account;
}

public abstract class CommandLineSecretVault(IExternalProcess processes, IExecutableResolver executables) : ISecretVault
{
    public abstract string DisplayName { get; }

    protected abstract string Executable { get; }

    public bool IsAvailable => executables.Resolve(Executable) is not null;

    public abstract Task<string?> ReadAsync(string service, string account, CancellationToken ct = default);

    public abstract Task<bool> WriteAsync(string service, string account, string secret, CancellationToken ct = default);

    public abstract Task<bool> RemoveAsync(string service, string account, CancellationToken ct = default);

    protected async Task<(int ExitCode, string Output)> RunAsync(
        IReadOnlyList<string> arguments, string? input, CancellationToken ct)
    {
        try
        {
            var outcome = await processes.RunAsync(executables.Resolve(Executable) ?? Executable, arguments, input: input, ct: ct)
                .ConfigureAwait(false);
            if (outcome is null) return (-1, string.Empty);
            return (outcome.ExitCode, outcome.Succeeded ? outcome.Output : outcome.Error);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return (-1, string.Empty);
        }
    }
}

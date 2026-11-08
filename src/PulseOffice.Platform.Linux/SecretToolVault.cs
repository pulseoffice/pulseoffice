using PulseOffice.Platform;

namespace PulseOffice.Platform.Linux;

public sealed class SecretToolVault(IExternalProcess processes, IExecutableResolver executables)
    : CommandLineSecretVault(processes, executables)
{
    private const string EndOfOptions = "--";

    public SecretToolVault()
        : this(new ExternalProcess(), new PathExecutableLocator())
    {
    }

    public override string DisplayName => "Secret Service";

    protected override string Executable => "secret-tool";

    public override async Task<string?> ReadAsync(string service, string account, CancellationToken ct = default)
    {
        var (code, output) = await RunAsync(["lookup", .. Attributes(service, account)], null, ct);

        return code == 0 && output.Length > 0 ? output : null;
    }

    public override async Task<bool> WriteAsync(
        string service, string account, string secret, CancellationToken ct = default)
    {
        var (code, _) = await RunAsync(
            ["store", $"--label={service} — {account}", .. Attributes(service, account)],
            secret,
            ct);

        return code == 0;
    }

    public override async Task<bool> RemoveAsync(string service, string account, CancellationToken ct = default)
    {
        var (code, _) = await RunAsync(["clear", .. Attributes(service, account)], null, ct);

        return code == 0;
    }

    private string[] Attributes(string service, string account) =>
        [EndOfOptions, "service", service, "account", account];
}

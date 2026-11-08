using PulseOffice.Platform;

namespace PulseOffice.Platform.MacOS;

public sealed class KeychainSecretVault(IExternalProcess processes, IExecutableResolver executables)
    : CommandLineSecretVault(processes, executables)
{
    public KeychainSecretVault()
        : this(new ExternalProcess(), new PathExecutableLocator())
    {
    }

    public override string DisplayName => "macOS Keychain";

    protected override string Executable => "/usr/bin/security";

    public override async Task<string?> ReadAsync(string service, string account, CancellationToken ct = default)
    {
        var (code, output) = await RunAsync(
            ["find-generic-password", "-s", service, "-a", account, "-w"], null, ct);

        return code == 0 ? KeychainText.Decode(output.EndsWith('\n') ? output[..^1] : output) : null;
    }

    public override async Task<bool> WriteAsync(
        string service, string account, string secret, CancellationToken ct = default)
    {
        if (!KeychainText.CanQuote(service) || !KeychainText.CanQuote(account)) return false;

        var (code, _) = await RunAsync(["-i"], KeychainText.AddCommand(service, account, secret), ct);

        return code == 0;
    }

    public override async Task<bool> RemoveAsync(string service, string account, CancellationToken ct = default)
    {
        var (code, _) = await RunAsync(
            ["delete-generic-password", "-s", service, "-a", account], null, ct);

        return code == 0;
    }
}

public static class KeychainText
{
    private static readonly System.Text.UTF8Encoding Strict = new(false, true);

    public static bool CanQuote(string value) => !value.Any(char.IsControl);

    public static string AddCommand(string service, string account, string secret)
    {
        if (!CanQuote(service) || !CanQuote(account))
            throw new ArgumentException("Keychain names cannot contain control characters.");

        return $"add-generic-password -U -s {Quote(service)} -a {Quote(account)} -X {Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(secret))}\n";
    }

    public static string Decode(string output)
    {
        if (output.Length == 0 || output.Length % 2 != 0 || !output.All(Uri.IsHexDigit)) return output;

        var bytes = Convert.FromHexString(output);
        if (!bytes.Any(value => value is >= 0x80 or < 0x20)) return output;

        try
        {
            return Strict.GetString(bytes);
        }
        catch (System.Text.DecoderFallbackException)
        {
            return output;
        }
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}

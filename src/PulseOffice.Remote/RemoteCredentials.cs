using PulseOffice.Platform;

namespace PulseOffice.Remote;

public interface IRemoteCredentials
{
    Task<string?> PasswordAsync(RemoteConnection connection, CancellationToken ct = default);

    Task KeepPasswordAsync(RemoteConnection connection, string password, CancellationToken ct = default);

    Task ForgetAsync(RemoteConnection connection, CancellationToken ct = default);
}

public sealed class RemoteCredentials(ISecretVaultProvider vaults, IEnumerable<IRemoteSignIn> signIns) : IRemoteCredentials
{
    private const string Service = "PulseOffice";

    public Task<string?> PasswordAsync(RemoteConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return vaults.Current.ReadAsync(Service, connection.SecretKey, ct);
    }

    public async Task KeepPasswordAsync(RemoteConnection connection, string password, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(password);

        await vaults.Current.WriteAsync(Service, connection.SecretKey, password, ct).ConfigureAwait(false);
    }

    public async Task ForgetAsync(RemoteConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        foreach (var signIn in signIns.Where(signIn => signIn.Handles(connection.Protocol)))
            await signIn.SignOutAsync(connection, ct).ConfigureAwait(false);

        await vaults.Current.RemoveAsync(Service, connection.SecretKey, ct).ConfigureAwait(false);
        await vaults.Current.RemoveAsync(Service, connection.TokenKey, ct).ConfigureAwait(false);
    }
}

public interface IRemoteFileNames
{
    bool IsValid(string? name);
}

public sealed class RemoteFileNames : IRemoteFileNames
{
    private const int MaximumLength = 255;

    public bool IsValid(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumLength) return false;
        if (name is "." or ".." || name != name.Trim()) return false;

        return !name.Any(character => character is '/' or '\\' || char.IsControl(character)
            || char.GetUnicodeCategory(character) is System.Globalization.UnicodeCategory.LineSeparator
                or System.Globalization.UnicodeCategory.ParagraphSeparator);
    }
}

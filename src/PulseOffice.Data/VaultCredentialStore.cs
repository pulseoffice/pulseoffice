using PulseOffice.Platform;

namespace PulseOffice.Data;

public sealed class VaultCredentialStore(ISecretVaultProvider vaults) : ICredentialStore
{
    private const string Service = "PulseOffice";

    public bool IsPersistent => vaults.Current.IsAvailable;

    public Task<string?> GetSecretAsync(string credentialKey, CancellationToken ct) =>
        vaults.Current.ReadAsync(Service, credentialKey, ct);

    public Task SetSecretAsync(string credentialKey, string secret, CancellationToken ct) =>
        vaults.Current.WriteAsync(Service, credentialKey, secret, ct);

    public Task RemoveAsync(string credentialKey, CancellationToken ct) =>
        vaults.Current.RemoveAsync(Service, credentialKey, ct);
}

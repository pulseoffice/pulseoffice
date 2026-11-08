using PulseOffice.Platform;
using PulseOffice.Platform.Linux;
using PulseOffice.Platform.MacOS;
using PulseOffice.Platform.Windows;

namespace PulseOffice.Desktop.Composition;

public sealed class SecretVaultProvider(IHostSystem host) : ISecretVaultProvider
{
    private readonly Lazy<ISecretVault> _vault = new(() => Select(host));

    public ISecretVault Current => _vault.Value;

    private static ISecretVault Select(IHostSystem host)
    {
        ISecretVault vault = host.IsMacOS
            ? new KeychainSecretVault()
            : host.IsWindows
                ? new CredentialManagerVault()
                : new SecretToolVault();

        return vault.IsAvailable ? vault : new SessionSecretVault();
    }
}

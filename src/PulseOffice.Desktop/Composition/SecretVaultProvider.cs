using PulseOffice.Platform;
using PulseOffice.Platform.Linux;
using PulseOffice.Platform.MacOS;
using PulseOffice.Platform.Windows;

namespace PulseOffice.Desktop.Composition;

public sealed class SecretVaultProvider(IHostSystem host, IExternalProcess processes, IExecutableResolver executables) : ISecretVaultProvider
{
    private ISecretVault? _vault;

    public ISecretVault Current => _vault ??= Select();

    private ISecretVault Select()
    {
        ISecretVault vault = host.IsMacOS
            ? new KeychainSecretVault(processes, executables)
            : host.IsWindows
                ? new CredentialManagerVault()
                : new SecretToolVault(processes, executables);

        return vault.IsAvailable ? vault : new SessionSecretVault();
    }
}

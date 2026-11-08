using System.Xml.Linq;
using PulseOffice.Formats.Packaging.Encryption.OpenPgp;

namespace PulseOffice.Formats.Packaging.Encryption;

public abstract record OdfUnlockKey
{
    internal abstract bool OpensOpenPgp { get; }

    internal abstract IStartKeySource Source(XDocument manifest, OdfKeyServices services);
}

public sealed record OdfPassword(string Password) : OdfUnlockKey
{
    internal override bool OpensOpenPgp => false;

    internal override IStartKeySource Source(XDocument manifest, OdfKeyServices services) =>
        new PasswordStartKeys(Password, services.StartKeys);
}

public sealed record OpenPgpSecretKey(byte[] KeyFile, string Passphrase) : OdfUnlockKey
{
    internal override bool OpensOpenPgp => true;

    internal override IStartKeySource Source(XDocument manifest, OdfKeyServices services) =>
        new OpenPgpStartKeys(this, services.Keys.Read(manifest), services.OpenPgp);
}

internal sealed record OdfKeyServices(IStartKeyCandidates StartKeys, OpenPgpManifestKeys Keys, IOpenPgp OpenPgp);

internal interface IStartKeySource
{
    OdfDecryptionStatus Failure { get; }

    IEnumerable<byte[]> Candidates(OdfEncryptionData probe);

    OdfProtection ProtectionFor(byte[] startKey);
}

internal sealed class PasswordStartKeys(string password, IStartKeyCandidates candidates) : IStartKeySource
{
    public OdfDecryptionStatus Failure => OdfDecryptionStatus.WrongPassword;

    public IEnumerable<byte[]> Candidates(OdfEncryptionData probe) =>
        probe.KeyDerivation == OdfCryptoNames.OpenPgpDerivation ? [] : candidates.For(probe.StartKeyAlgorithm, password);

    public OdfProtection ProtectionFor(byte[] startKey) => new PasswordProtection(password);
}

internal sealed class OpenPgpStartKeys(OpenPgpSecretKey key, IReadOnlyList<OpenPgpKeySlot> slots, IOpenPgp openPgp) : IStartKeySource
{
    public OdfDecryptionStatus Failure { get; private set; } = OdfDecryptionStatus.NoMatchingKey;

    public IEnumerable<byte[]> Candidates(OdfEncryptionData probe)
    {
        if (slots.Count == 0)
        {
            Failure = OdfDecryptionStatus.Unsupported;
            yield break;
        }

        var unlock = openPgp.Decrypt([.. slots.Select(slot => slot.CipherValue)], key.KeyFile, key.Passphrase);
        Failure = unlock.Status switch
        {
            OpenPgpUnlockStatus.WrongPassphrase => OdfDecryptionStatus.WrongPassword,
            OpenPgpUnlockStatus.InvalidKeyFile => OdfDecryptionStatus.InvalidKeyFile,
            OpenPgpUnlockStatus.Decrypted => OdfDecryptionStatus.Unsupported,
            _ => OdfDecryptionStatus.NoMatchingKey,
        };
        if (unlock.Plain is { } sessionKey) yield return sessionKey;
    }

    public OdfProtection ProtectionFor(byte[] startKey) => new OpenPgpProtection(startKey, slots);
}

namespace PulseOffice.Formats.Packaging.Encryption.OpenPgp;

public sealed record OpenPgpRecipient(string UserId, string Fingerprint, byte[] PublicKeyRing);

public sealed record OpenPgpKeySlot(byte[] KeyId, byte[]? KeyPacket, byte[] CipherValue);

public enum OpenPgpUnlockStatus { Decrypted, WrongPassphrase, NoMatchingKey, InvalidKeyFile }

public sealed record OpenPgpUnlock(OpenPgpUnlockStatus Status, byte[]? Plain);

public interface IOpenPgp
{
    OpenPgpUnlock Decrypt(IReadOnlyList<byte[]> messages, byte[] secretKeyFile, string passphrase);

    IReadOnlyList<OpenPgpRecipient> ReadRecipients(byte[] publicKeyFile);

    byte[] Encrypt(byte[] plain, OpenPgpRecipient recipient);
}

public interface IOpenPgpProtectionFactory
{
    OpenPgpProtection ForRecipients(IReadOnlyList<OpenPgpRecipient> recipients);
}

public sealed class OpenPgpProtectionFactory(IOpenPgp openPgp) : IOpenPgpProtectionFactory
{
    private const int SessionKeySize = 32;

    public OpenPgpProtection ForRecipients(IReadOnlyList<OpenPgpRecipient> recipients)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        if (recipients.Count == 0) throw new ArgumentException("At least one recipient is required.", nameof(recipients));

        var sessionKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(SessionKeySize);
        var slots = recipients.Select(recipient =>
        {
            var keyId = System.Text.Encoding.ASCII.GetBytes(recipient.Fingerprint + "\0");
            return new OpenPgpKeySlot(keyId, keyId, openPgp.Encrypt(sessionKey, recipient));
        }).ToList();
        return new OpenPgpProtection(sessionKey, slots);
    }
}

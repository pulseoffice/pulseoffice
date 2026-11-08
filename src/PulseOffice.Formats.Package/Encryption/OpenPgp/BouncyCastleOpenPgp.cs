using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using System.Security.Cryptography;
using Org.BouncyCastle.Security;

namespace PulseOffice.Formats.Packaging.Encryption.OpenPgp;

public sealed class BouncyCastleOpenPgp : IOpenPgp
{
    private const int MaxPlainBytes = 4096;
    private const long MaxKeyFileBytes = 16L * 1024 * 1024;

    public OpenPgpUnlock Decrypt(IReadOnlyList<byte[]> messages, byte[] secretKeyFile, string passphrase)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(secretKeyFile);
        ArgumentNullException.ThrowIfNull(passphrase);

        if (SecretKeys(secretKeyFile) is not { Count: > 0 } bundle) return new OpenPgpUnlock(OpenPgpUnlockStatus.InvalidKeyFile, null);

        var status = OpenPgpUnlockStatus.NoMatchingKey;
        var secretChars = passphrase.ToCharArray();
        try
        {
            foreach (var data in messages.SelectMany(EncryptedData))
            {
                if (bundle.GetSecretKey(data.KeyId) is not { } secret) continue;
                PgpPrivateKey? key;
                try
                {
                    key = secret.ExtractPrivateKey(secretChars);
                }
                catch (Exception ex) when (ex is PgpException or ArgumentException or InvalidOperationException)
                {
                    status = OpenPgpUnlockStatus.WrongPassphrase;
                    continue;
                }

                if (key is not null && Open(data, key) is { } plain) return new OpenPgpUnlock(OpenPgpUnlockStatus.Decrypted, plain);
            }
        }
        finally
        {
            Array.Clear(secretChars);
        }

        return new OpenPgpUnlock(status, null);
    }

    public IReadOnlyList<OpenPgpRecipient> ReadRecipients(byte[] publicKeyFile)
    {
        ArgumentNullException.ThrowIfNull(publicKeyFile);
        if (publicKeyFile.LongLength > MaxKeyFileBytes) throw new InvalidDataException("The key file is too large.");

        PgpPublicKeyRingBundle bundle;
        try
        {
            bundle = new PgpPublicKeyRingBundle(PgpUtilities.GetDecoderStream(new MemoryStream(publicKeyFile)));
        }
        catch (Exception ex) when (ex is IOException or PgpException or ArgumentException)
        {
            throw new InvalidDataException("The file does not contain OpenPGP public keys.", ex);
        }

        var recipients = new List<OpenPgpRecipient>();
        foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
        {
            if (EncryptionKey(ring) is null) continue;
            var primary = ring.GetPublicKey();
            var userId = primary.GetUserIds().FirstOrDefault() ?? primary.KeyId.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
            recipients.Add(new OpenPgpRecipient(userId, Convert.ToHexString(primary.GetFingerprint()), ring.GetEncoded()));
        }

        return recipients;
    }

    public byte[] Encrypt(byte[] plain, OpenPgpRecipient recipient)
    {
        ArgumentNullException.ThrowIfNull(plain);
        ArgumentNullException.ThrowIfNull(recipient);

        var key = EncryptionKey(new PgpPublicKeyRing(recipient.PublicKeyRing))
            ?? throw new InvalidDataException("The recipient has no usable encryption key.");

        using var literal = new MemoryStream();
        using (var stream = new PgpLiteralDataGenerator().Open(literal, PgpLiteralData.Binary, string.Empty, plain.Length, DateTime.UtcNow))
            stream.Write(plain);
        var literalBytes = literal.ToArray();
        try
        {
            var generator = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithmTag.Aes256, true, new SecureRandom());
            generator.AddMethod(key);
            using var output = new MemoryStream();
            using (var encrypted = generator.Open(output, literalBytes.Length)) encrypted.Write(literalBytes);
            return output.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(literalBytes);
            CryptographicOperations.ZeroMemory(literal.GetBuffer());
        }
    }

    private PgpSecretKeyRingBundle? SecretKeys(byte[] file)
    {
        if (file.LongLength > MaxKeyFileBytes) return null;
        try
        {
            return new PgpSecretKeyRingBundle(PgpUtilities.GetDecoderStream(new MemoryStream(file)));
        }
        catch (Exception ex) when (ex is IOException or PgpException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private IEnumerable<PgpPublicKeyEncryptedData> EncryptedData(byte[] message)
    {
        try
        {
            var factory = new PgpObjectFactory(PgpUtilities.GetDecoderStream(new MemoryStream(message)));
            for (var item = factory.NextPgpObject(); item is not null; item = factory.NextPgpObject())
                if (item is PgpEncryptedDataList list)
                    return [.. list.GetEncryptedDataObjects().OfType<PgpPublicKeyEncryptedData>()];
        }
        catch (Exception ex) when (ex is IOException or PgpException or ArgumentException)
        {
        }

        return [];
    }

    private byte[]? Open(PgpPublicKeyEncryptedData data, PgpPrivateKey key)
    {
        try
        {
            if (!data.IsIntegrityProtected()) return null;
            using var clear = data.GetDataStream(key);
            var plain = Literal(new PgpObjectFactory(clear));
            if (plain is null) return null;
            if (data.Verify()) return plain;
            CryptographicOperations.ZeroMemory(plain);
            return null;
        }
        catch (Exception ex) when (ex is IOException or PgpException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private byte[]? Literal(PgpObjectFactory factory)
    {
        for (var item = factory.NextPgpObject(); item is not null; item = factory.NextPgpObject())
        {
            switch (item)
            {
                case PgpCompressedData compressed:
                    return Literal(new PgpObjectFactory(compressed.GetDataStream()));
                case PgpLiteralData literal:
                    using (var input = literal.GetInputStream())
                    using (var output = new MemoryStream())
                    {
                        var buffer = new byte[1024];
                        try
                        {
                            int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                output.Write(buffer, 0, read);
                                if (output.Length > MaxPlainBytes) return null;
                            }

                            return output.ToArray();
                        }
                        finally
                        {
                            CryptographicOperations.ZeroMemory(buffer);
                            CryptographicOperations.ZeroMemory(output.GetBuffer());
                        }
                    }
            }
        }

        return null;
    }

    private PgpPublicKey? EncryptionKey(PgpPublicKeyRing ring) =>
        ring.GetPublicKeys()
            .Where(key => key.IsEncryptionKey && !key.HasRevocation() && !Expired(key))
            .OrderBy(key => key.IsMasterKey ? 1 : 0)
            .FirstOrDefault();

    private bool Expired(PgpPublicKey key)
    {
        var seconds = key.GetValidSeconds();
        return seconds > 0 && key.CreationTime.AddSeconds(seconds) < DateTime.UtcNow;
    }
}

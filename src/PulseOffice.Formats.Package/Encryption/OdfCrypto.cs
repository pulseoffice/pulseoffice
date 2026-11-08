using System.Security.Cryptography;
using System.Text;

namespace PulseOffice.Formats.Packaging.Encryption;

internal static class OdfCryptoNames
{
    public const string AesCbc = "http://www.w3.org/2001/04/xmlenc#aes256-cbc";
    public const string AesCbc192 = "http://www.w3.org/2001/04/xmlenc#aes192-cbc";
    public const string AesCbc128 = "http://www.w3.org/2001/04/xmlenc#aes128-cbc";
    public const string AesGcm = "http://www.w3.org/2009/xmlenc11#aes256-gcm";
    public const string Blowfish = "Blowfish CFB";
    public const string Sha256Checksum = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#sha256-1k";
    public const string Sha256StartKey = "http://www.w3.org/2000/09/xmldsig#sha256";
    public const string Argon2id = "urn:org:documentfoundation:names:experimental:office:manifest:argon2id";
    public const string OpenPgpDerivation = "PGP";
}

public interface IStartKeyCandidates
{
    IEnumerable<byte[]> For(string? algorithm, string password);
}

internal sealed class StartKeyCandidates : IStartKeyCandidates
{
    public IEnumerable<byte[]> For(string? algorithm, string password)
    {
        var utf8 = Encoding.UTF8.GetBytes(password);
        var latin1 = Encoding.Latin1.GetBytes(password);
        try
        {
            switch (algorithm)
            {
                case "SHA256" or OdfCryptoNames.Sha256StartKey or "http://www.w3.org/2001/04/xmlenc#sha256":
                    yield return SHA256.HashData(utf8);
                    yield return SHA1.HashData(utf8);
                    break;
                case "SHA512" or "http://www.w3.org/2001/04/xmlenc#sha512":
                    yield return SHA512.HashData(utf8);
                    break;
                default:
                    yield return SHA1.HashData(utf8);
                    yield return SHA1.HashData(latin1);
                    yield return SHA256.HashData(utf8);
                    break;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(utf8);
            CryptographicOperations.ZeroMemory(latin1);
        }
    }
}

internal interface IKeyDerivation
{
    bool Handles(OdfEncryptionData data);

    byte[] Derive(byte[] startKey, OdfEncryptionData data);
}

internal sealed class Pbkdf2Derivation : IKeyDerivation
{
    public bool Handles(OdfEncryptionData data) => data.Argon2 is null && data.KeyDerivation != OdfCryptoNames.OpenPgpDerivation;

    public byte[] Derive(byte[] startKey, OdfEncryptionData data)
    {
        if (data.IterationCount is < 1 or > 10_000_000 || data.KeySize is < 1 or > 64)
            throw new InvalidDataException("Unsupported key derivation parameters.");
        return Rfc2898DeriveBytes.Pbkdf2(startKey, data.Salt, data.IterationCount, HashAlgorithmName.SHA1, data.KeySize);
    }
}

internal sealed class SessionKeyDerivation : IKeyDerivation
{
    public bool Handles(OdfEncryptionData data) => data.KeyDerivation == OdfCryptoNames.OpenPgpDerivation;

    public byte[] Derive(byte[] startKey, OdfEncryptionData data) =>
        startKey.Length == data.KeySize ? startKey : throw new InvalidDataException("Unexpected OpenPGP session key size.");
}

internal sealed class Argon2Derivation(Argon2id argon) : IKeyDerivation
{
    public bool Handles(OdfEncryptionData data) => data.Argon2 is not null;

    public byte[] Derive(byte[] startKey, OdfEncryptionData data) =>
        argon.Derive(startKey, data.Salt, data.Argon2!.Iterations, data.Argon2.MemoryKiB, data.Argon2.Lanes, data.KeySize);
}

internal interface IOdfCipher
{
    bool Handles(string algorithm);

    byte[]? Decrypt(byte[] key, byte[] iv, byte[] data);

    byte[] Encrypt(byte[] key, byte[] iv, byte[] data);
}

internal sealed class AesCbcCipher : IOdfCipher
{
    public bool Handles(string algorithm) => algorithm is OdfCryptoNames.AesCbc or OdfCryptoNames.AesCbc192 or OdfCryptoNames.AesCbc128;

    public byte[]? Decrypt(byte[] key, byte[] iv, byte[] data)
    {
        if (data.Length == 0 || data.Length % 16 != 0 || iv.Length != 16) return null;
        using var aes = Aes.Create();
        aes.Key = key;
        var plain = aes.DecryptCbc(data, iv, PaddingMode.None);
        var padding = plain[^1];
        return padding is < 1 or > 16 ? null : plain[..^padding];
    }

    public byte[] Encrypt(byte[] key, byte[] iv, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.EncryptCbc(data, iv, PaddingMode.ISO10126);
    }
}

internal sealed class AesGcmCipher : IOdfCipher
{
    private const int TagSize = 16;
    private const int IvSize = 12;

    public bool Handles(string algorithm) => algorithm == OdfCryptoNames.AesGcm;

    public byte[]? Decrypt(byte[] key, byte[] iv, byte[] data)
    {
        if (data.Length < IvSize + TagSize || iv.Length != IvSize || !data.AsSpan(0, IvSize).SequenceEqual(iv)) return null;
        var plain = new byte[data.Length - IvSize - TagSize];
        try
        {
            using var gcm = new AesGcm(key, TagSize);
            gcm.Decrypt(iv, data.AsSpan(IvSize, plain.Length), data.AsSpan(IvSize + plain.Length), plain);
            return plain;
        }
        catch (AuthenticationTagMismatchException)
        {
            return null;
        }
    }

    public byte[] Encrypt(byte[] key, byte[] iv, byte[] data)
    {
        var output = new byte[IvSize + data.Length + TagSize];
        iv.CopyTo(output, 0);
        using var gcm = new AesGcm(key, TagSize);
        gcm.Encrypt(iv, data, output.AsSpan(IvSize, data.Length), output.AsSpan(IvSize + data.Length));
        return output;
    }
}

internal sealed class BlowfishCfbCipher(BlowfishTables tables) : IOdfCipher
{
    public bool Handles(string algorithm) => algorithm is OdfCryptoNames.Blowfish or "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#blowfish";

    public byte[]? Decrypt(byte[] key, byte[] iv, byte[] data) => Transform(key, iv, data, decrypting: true);

    public byte[] Encrypt(byte[] key, byte[] iv, byte[] data) => Transform(key, iv, data, decrypting: false);

    private byte[] Transform(byte[] key, byte[] iv, byte[] data, bool decrypting)
    {
        if (iv.Length != 8) throw new InvalidDataException("Invalid Blowfish initialisation vector.");
        var cipher = new Blowfish(tables, key);
        var register = (byte[])iv.Clone();
        var keystream = new byte[8];
        var output = new byte[data.Length];
        for (var offset = 0; offset < data.Length; offset += 8)
        {
            register.CopyTo(keystream, 0);
            cipher.EncryptBlock(keystream);
            var count = Math.Min(8, data.Length - offset);
            for (var k = 0; k < count; k++)
            {
                output[offset + k] = (byte)(data[offset + k] ^ keystream[k]);
                register[k] = decrypting ? data[offset + k] : output[offset + k];
            }
        }

        return output;
    }
}

internal sealed class OdfChecksum
{
    private const int Limit = 1024;

    public bool Matches(OdfEncryptionData data, byte[] decrypted)
    {
        if (data.Checksum is null || data.ChecksumType is null) return true;
        var span = decrypted.AsSpan(0, Math.Min(Limit, decrypted.Length));
        var full = decrypted.AsSpan();
        var digest = data.ChecksumType switch
        {
            OdfCryptoNames.Sha256Checksum or "SHA256/1K" => SHA256.HashData(span),
            "SHA1/1K" or "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#sha1-1k" => SHA1.HashData(span),
            "SHA1" => SHA1.HashData(full),
            _ => null,
        };
        return digest is null || CryptographicOperations.FixedTimeEquals(digest, data.Checksum);
    }

    public byte[] Of(byte[] encryptedSource) => SHA256.HashData(encryptedSource.AsSpan(0, Math.Min(Limit, encryptedSource.Length)));
}

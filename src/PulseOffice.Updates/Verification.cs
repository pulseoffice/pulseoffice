using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PulseOffice.Updates;

public sealed class UpdateVerificationException(string message) : Exception(message);

public interface IUpdateTrust
{
    bool IsConfigured { get; }

    bool Verifies(ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature);
}

public sealed class EcdsaUpdateTrust : IUpdateTrust, IDisposable
{
    private readonly ECDsa? _key;

    public EcdsaUpdateTrust(IUpdateTrustKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.PublicKeyPem() is not { Length: > 0 } pem) return;
        var loaded = ECDsa.Create();
        loaded.ImportFromPem(pem);
        if (loaded.KeySize != 256)
        {
            loaded.Dispose();
            throw new CryptographicException("The update key must be an ECDSA P-256 public key.");
        }
        _key = loaded;
    }

    public bool IsConfigured => _key is not null;

    public bool Verifies(ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature) =>
        _key is { } key && key.VerifyData(content, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

    public void Dispose() => _key?.Dispose();
}

public interface IUpdateTrustKey
{
    string? PublicKeyPem();
}

public sealed class EmbeddedUpdateTrustKey : IUpdateTrustKey
{
    private const string ResourceName = "PulseOffice.Updates.update-key.pem";

    public string? PublicKeyPem()
    {
        using var stream = typeof(EmbeddedUpdateTrustKey).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream, Encoding.ASCII);
        return reader.ReadToEnd();
    }
}

public sealed class FixedUpdateTrustKey(string? pem) : IUpdateTrustKey
{
    public string? PublicKeyPem() => pem;
}

public interface IChecksumList
{
    IReadOnlyDictionary<string, byte[]> Read(string text);
}

public sealed class Sha256SumsList : IChecksumList
{
    private const int HashLength = 32;

    public IReadOnlyDictionary<string, byte[]> Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length < HashLength * 2 + 2) continue;
            var hex = line[..(HashLength * 2)];
            var name = line[(HashLength * 2)..].TrimStart(' ', '*');
            if (name.Length == 0 || name.Contains('/') || name.Contains('\\')) continue;
            byte[] hash;
            try
            {
                hash = Convert.FromHexString(hex);
            }
            catch (FormatException)
            {
                continue;
            }
            if (!entries.TryAdd(name, hash)) throw new UpdateVerificationException(string.Create(CultureInfo.InvariantCulture, $"The checksum list names {name} twice."));
        }
        return entries;
    }
}

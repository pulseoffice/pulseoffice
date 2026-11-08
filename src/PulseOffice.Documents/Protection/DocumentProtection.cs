using System.Security.Cryptography;
using System.Text;

namespace PulseOffice.Documents.Protection;

public enum DocumentRestriction
{
    None,
    ReadOnly,
    Comments,
    TrackedChanges,
    Forms,
}

public sealed record ProtectionKey(string Algorithm, byte[] Hash)
{
    public const string Odf = "odf";

    public byte[] Salt { get; init; } = [];

    public int SpinCount { get; init; }

    public bool IsWord => Algorithm != Odf;
}

public sealed record MarkProtection(bool Fields, bool Bookmarks)
{
    public static readonly MarkProtection None = new(false, false);
}

public sealed record DocumentProtection(DocumentRestriction Restriction, ProtectionKey? Key)
{
    public static readonly DocumentProtection None = new(DocumentRestriction.None, null);

    public bool IsActive => Restriction != DocumentRestriction.None;
}

public interface IProtectionKeys
{
    ProtectionKey Create(string password);

    byte[] Digest(string password);

    bool Matches(ProtectionKey? key, string? password);

    bool Matches(byte[]? digest, string? password);
}

public sealed class ProtectionKeys : IProtectionKeys
{
    private const int MaxSpinCount = 10_000_000;

    public ProtectionKey Create(string password) => new(ProtectionKey.Odf, Digest(password));

    public byte[] Digest(string password) => SHA256.HashData(Encoding.UTF8.GetBytes(password));

    public bool Matches(byte[]? digest, string? password)
    {
        if (digest is null) return true;
        if (password is null) return false;
        return digest.Length switch
        {
            20 => Same(SHA1.HashData(Encoding.UTF8.GetBytes(password)), digest)
                  || Same(SHA1.HashData(Encoding.BigEndianUnicode.GetBytes(password)), digest)
                  || Same(SHA1.HashData(Encoding.Unicode.GetBytes(password)), digest),
            32 => Same(SHA256.HashData(Encoding.UTF8.GetBytes(password)), digest),
            _ => false,
        };
    }

    public bool Matches(ProtectionKey? key, string? password)
    {
        if (key is null) return true;
        if (password is null) return false;
        if (!key.IsWord) return Matches(key.Hash, password);
        if (key.SpinCount is < 0 or > MaxSpinCount) return false;
        using var algorithm = Algorithm(key.Algorithm);
        if (algorithm is null) return false;
        var current = algorithm.ComputeHash([.. key.Salt, .. Encoding.Unicode.GetBytes(password)]);
        var buffer = new byte[current.Length + 4];
        for (var iteration = 0; iteration < key.SpinCount; iteration++)
        {
            current.CopyTo(buffer, 0);
            BitConverter.TryWriteBytes(buffer.AsSpan(current.Length), iteration);
            current = algorithm.ComputeHash(buffer);
        }
        return Same(current, key.Hash);
    }

    private HashAlgorithm? Algorithm(string name) => name.ToUpperInvariant() switch
    {
        "SHA-512" => SHA512.Create(),
        "SHA-384" => SHA384.Create(),
        "SHA-256" => SHA256.Create(),
        "SHA-1" => SHA1.Create(),
        _ => null,
    };

    private bool Same(byte[] first, byte[] second) => CryptographicOperations.FixedTimeEquals(first, second);
}

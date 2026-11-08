using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PulseOffice.Sheets;

public abstract class PasswordKey
{
    public const string OoxmlPrefix = "ooxml:";
    public const string OoxmlLegacyPrefix = "ooxml-legacy:";
    public const string OdfPrefix = "odf:";

    public abstract bool Matches(string password);

    public static PasswordKey Parse(string hash) =>
        hash.StartsWith(OoxmlLegacyPrefix, StringComparison.Ordinal) ? new LegacyExcelKey(hash[OoxmlLegacyPrefix.Length..])
        : hash.StartsWith(OoxmlPrefix, StringComparison.Ordinal) ? AgileExcelKey.From(hash[OoxmlPrefix.Length..])
        : hash.StartsWith(OdfPrefix, StringComparison.Ordinal) ? OdfDigestKey.From(hash[OdfPrefix.Length..])
        : new NativeKey(hash);
}

public sealed class NativeKey(string hash) : PasswordKey
{
    public override bool Matches(string password) => WorkbookProtection.Hash(password) == hash;
}

public sealed class UnverifiableKey : PasswordKey
{
    public override bool Matches(string password) => false;
}

public sealed class LegacyExcelKey(string hex) : PasswordKey
{
    public override bool Matches(string password) =>
        int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var expected) && new LegacyExcelHash().Of(password) == expected;
}

public sealed class LegacyExcelHash
{
    public int Of(string password)
    {
        var bytes = Encoding.Latin1.GetBytes(password);
        var hash = 0;
        for (var index = bytes.Length - 1; index >= 0; index--)
        {
            hash = ((hash >> 14) & 0x01) | ((hash << 1) & 0x7FFF);
            hash ^= bytes[index];
        }

        hash = ((hash >> 14) & 0x01) | ((hash << 1) & 0x7FFF);
        return hash ^ bytes.Length ^ 0xCE4B;
    }
}

public sealed class AgileExcelKey(string algorithm, int spinCount, byte[] salt, byte[] hash) : PasswordKey
{
    private const int MaxSpinCount = 10_000_000;

    public static PasswordKey From(string text)
    {
        var parts = text.Split(':');
        if (parts.Length != 4 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var spin) || spin is < 0 or > MaxSpinCount)
            return new UnverifiableKey();
        try
        {
            return new AgileExcelKey(parts[0], spin, Convert.FromBase64String(parts[2]), Convert.FromBase64String(parts[3]));
        }
        catch (FormatException)
        {
            return new UnverifiableKey();
        }
    }

    public override bool Matches(string password)
    {
        using var algorithmInstance = Create();
        if (algorithmInstance is null) return false;

        var current = algorithmInstance.ComputeHash([.. salt, .. Encoding.Unicode.GetBytes(password)]);
        var buffer = new byte[current.Length + 4];
        for (var iteration = 0; iteration < spinCount; iteration++)
        {
            current.CopyTo(buffer, 0);
            BitConverter.TryWriteBytes(buffer.AsSpan(current.Length), iteration);
            current = algorithmInstance.ComputeHash(buffer);
        }

        return CryptographicOperations.FixedTimeEquals(current, hash);
    }

    private HashAlgorithm? Create() => algorithm.ToUpperInvariant() switch
    {
        "SHA-512" => SHA512.Create(),
        "SHA-384" => SHA384.Create(),
        "SHA-256" => SHA256.Create(),
        "SHA-1" => SHA1.Create(),
        _ => null,
    };
}

public sealed class OdfDigestKey(string algorithm, string secondAlgorithm, string key) : PasswordKey
{
    private const string Sha1 = "http://www.w3.org/2000/09/xmldsig#sha1";
    private const string Sha256 = "http://www.w3.org/2000/09/xmldsig#sha256";
    private const string Sha256Encryption = "http://www.w3.org/2001/04/xmlenc#sha256";
    private const string ExcelLegacy = "http://docs.oasis-open.org/office/ns/table/legacy-hash-excel";

    public static PasswordKey From(string text)
    {
        var parts = text.Split('|');
        return parts.Length switch
        {
            2 => new OdfDigestKey(parts[0], string.Empty, parts[1]),
            3 => new OdfDigestKey(parts[0], parts[1], parts[2]),
            _ => new UnverifiableKey(),
        };
    }

    public override bool Matches(string password) =>
        FirstStage(password).Any(bytes => SecondStage(bytes) is { } digest && Convert.ToBase64String(digest) == key);

    private IEnumerable<byte[]> FirstStage(string password)
    {
        switch (algorithm)
        {
            case "" or Sha1:
                yield return SHA1.HashData(Encoding.UTF8.GetBytes(password));
                yield return SHA1.HashData(Encoding.Unicode.GetBytes(password));
                break;
            case Sha256 or Sha256Encryption:
                yield return SHA256.HashData(Encoding.UTF8.GetBytes(password));
                break;
            case ExcelLegacy:
                var hash = new LegacyExcelHash().Of(password);
                yield return [(byte)(hash >> 8), (byte)hash];
                break;
        }
    }

    private byte[]? SecondStage(byte[] first) => secondAlgorithm switch
    {
        "" => first,
        Sha1 => SHA1.HashData(first),
        Sha256 or Sha256Encryption => SHA256.HashData(first),
        _ => null,
    };
}

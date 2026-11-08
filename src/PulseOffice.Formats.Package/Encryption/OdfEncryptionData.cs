using System.Xml.Linq;

namespace PulseOffice.Formats.Packaging.Encryption;

public sealed record OdfEncryptionData(
    string Algorithm,
    byte[] InitialisationVector,
    string? ChecksumType,
    byte[]? Checksum,
    string? StartKeyAlgorithm,
    string KeyDerivation,
    int KeySize,
    int IterationCount,
    byte[] Salt,
    Argon2Parameters? Argon2,
    long? Size);

public sealed record Argon2Parameters(int Iterations, int MemoryKiB, int Lanes);

internal sealed class OdfEncryptionDataReader
{
    private static readonly XNamespace Manifest = OdfPackage.Namespaces.Manifest;
    private static readonly XNamespace Loext = OdfPackage.Namespaces.Loext;

    public IReadOnlyDictionary<string, OdfEncryptionData> Read(XDocument manifest)
    {
        var result = new Dictionary<string, OdfEncryptionData>(StringComparer.Ordinal);
        foreach (var entry in manifest.Root?.Elements(Manifest + "file-entry") ?? [])
        {
            if (entry.Element(Manifest + "encryption-data") is not { } data) continue;
            var path = (string?)entry.Attribute(Manifest + "full-path");
            if (string.IsNullOrEmpty(path)) continue;
            result[path] = Parse(data, entry);
        }

        return result;
    }

    private OdfEncryptionData Parse(XElement data, XElement entry)
    {
        var algorithm = data.Element(Manifest + "algorithm");
        var derivation = data.Element(Manifest + "key-derivation");
        var start = data.Element(Manifest + "start-key-generation");
        var argon = derivation?.Attribute(Loext + "argon2-iterations") is { } iterations
            ? new Argon2Parameters(Int(iterations), Int(derivation.Attribute(Loext + "argon2-memory")), Int(derivation.Attribute(Loext + "argon2-lanes")))
            : null;

        var algorithmName = (string?)algorithm?.Attribute(Manifest + "algorithm-name") ?? "Blowfish CFB";
        return new OdfEncryptionData(
            algorithmName,
            Bytes(algorithm?.Attribute(Manifest + "initialisation-vector")) ?? [],
            (string?)data.Attribute(Manifest + "checksum-type"),
            Bytes(data.Attribute(Manifest + "checksum")),
            (string?)start?.Attribute(Manifest + "start-key-generation-name"),
            (string?)derivation?.Attribute(Manifest + "key-derivation-name") ?? "PBKDF2",
            derivation?.Attribute(Manifest + "key-size") is { } size ? Int(size) : DefaultKeySize(algorithmName),
            derivation?.Attribute(Manifest + "iteration-count") is { } count ? Int(count) : 0,
            Bytes(derivation?.Attribute(Manifest + "salt")) ?? [],
            argon,
            long.TryParse((string?)entry.Attribute(Manifest + "size"), out var plainSize) ? plainSize : null);
    }

    private int DefaultKeySize(string algorithm) =>
        algorithm.Contains("aes256", StringComparison.OrdinalIgnoreCase) ? 32
        : algorithm.Contains("aes192", StringComparison.OrdinalIgnoreCase) ? 24
        : 16;

    private int Int(XAttribute? attribute) =>
        int.TryParse(attribute?.Value, out var value) ? value : throw new InvalidDataException("Invalid encryption parameter.");

    private byte[]? Bytes(XAttribute? attribute)
    {
        if (attribute is null) return null;
        try
        {
            return Convert.FromBase64String(attribute.Value);
        }
        catch (FormatException)
        {
            throw new InvalidDataException("Invalid encryption parameter.");
        }
    }
}

using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace PulseOffice.Formats.Packaging.Encryption;

public enum OdfDecryptionStatus { Decrypted, WrongPassword, Unsupported, RequiresOpenPgp, NoMatchingKey, InvalidKeyFile }

public sealed record OdfDecryptionResult(OdfDecryptionStatus Status, MemoryStream? Package, OdfProtection? Protection = null);

public interface IOdfPackageEncryption
{
    bool IsEncrypted(Stream stream);

    bool RequiresOpenPgp(Stream stream);

    OdfDecryptionResult Decrypt(Stream stream, OdfUnlockKey key);

    MemoryStream Encrypt(Stream package, OdfProtection protection);

    OdfDecryptionResult Decrypt(Stream stream, string password) => Decrypt(stream, new OdfPassword(password));

    MemoryStream Encrypt(Stream package, string password) => Encrypt(package, new PasswordProtection(password));
}

public sealed class OdfPackageEncryption : IOdfPackageEncryption
{
    private const string ManifestPath = "META-INF/manifest.xml";
    private const string WrapperPath = "encrypted-package";
    private const long MaxEntryBytes = 512L * 1024 * 1024;
    private const long MaxPackageBytes = 1L << 30;
    private static readonly XNamespace Manifest = OdfPackage.Namespaces.Manifest;

    private readonly OdfEncryptionDataReader _reader = new();
    private readonly IStartKeyCandidates _startKeys;
    private readonly IReadOnlyList<IKeyDerivation> _derivations = [new Argon2Derivation(new Argon2id()), new SessionKeyDerivation(), new Pbkdf2Derivation()];
    private readonly IReadOnlyList<IOdfCipher> _ciphers = [new AesCbcCipher(), new AesGcmCipher(), new BlowfishCfbCipher(new BlowfishTables())];
    private readonly OdfChecksum _checksum = new();
    private readonly XmlPartLoader _xml = new();
    private readonly OpenPgp.OpenPgpManifestKeys _openPgpKeys = new();
    private readonly Legacy.ILegacyOdfUpgrader _legacy;
    private readonly OpenPgp.IOpenPgp _openPgp;
    private readonly OdfDerivationBudget _budget;

    public OdfPackageEncryption() : this(new Legacy.LegacyOdfUpgrader(), new OpenPgp.BouncyCastleOpenPgp())
    {
    }

    public OdfPackageEncryption(Legacy.ILegacyOdfUpgrader legacy, OpenPgp.IOpenPgp openPgp, IStartKeyCandidates? startKeys = null, OdfDerivationBudget? budget = null)
    {
        _startKeys = startKeys ?? new StartKeyCandidates();
        _budget = budget ?? new OdfDerivationBudget();
        _legacy = legacy;
        _openPgp = openPgp;
    }

    public bool IsEncrypted(Stream stream) =>
        ProbeManifest(stream, manifest => manifest.Descendants().Any(element => element.Name.LocalName == "encryption-data"
            && (element.Name.Namespace == Manifest || element.Name.Namespace == Legacy.LegacyVocabulary.LegacyManifest)));

    public bool RequiresOpenPgp(Stream stream) =>
        ProbeManifest(stream, IsOpenPgp);

    private bool IsOpenPgp(XDocument manifest) => _openPgpKeys.IsPresent(manifest);

    private bool ProbeManifest(Stream stream, Func<XDocument, bool> test)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek) return false;
        var origin = stream.Position;
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.GetEntry(ManifestPath) is not { } entry) return false;
            using var input = entry.Open();
            return test(_xml.Load(input, LoadOptions.None));
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or IOException)
        {
            return false;
        }
        finally
        {
            stream.Position = origin;
        }
    }

    public OdfDecryptionResult Decrypt(Stream stream, OdfUnlockKey key)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(key);

        if (_legacy.IsLegacy(stream))
        {
            using var upgraded = _legacy.Upgrade(stream);
            return Decrypt(upgraded, key);
        }

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = archive.GetEntry(ManifestPath) ?? throw new InvalidDataException("The package has no manifest.");
        XDocument manifest;
        try
        {
            using var input = manifestEntry.Open();
            manifest = _xml.Load(input, LoadOptions.None);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new InvalidDataException("The package manifest is not well-formed.", ex);
        }

        var openPgp = IsOpenPgp(manifest);
        if (openPgp != key.OpensOpenPgp)
            return new OdfDecryptionResult(openPgp ? OdfDecryptionStatus.RequiresOpenPgp : OdfDecryptionStatus.Unsupported, null);
        var source = key.Source(manifest, new OdfKeyServices(_startKeys, _openPgpKeys, _openPgp));
        var encrypted = _reader.Read(manifest);
        if (encrypted.Count == 0 || !_budget.Allows(encrypted.Values)) return new OdfDecryptionResult(OdfDecryptionStatus.Unsupported, null);

        var probe = encrypted.ContainsKey(WrapperPath) ? WrapperPath : encrypted.OrderBy(pair => pair.Key == "content.xml" ? 0 : 1).First().Key;
        var probeData = encrypted[probe];
        if (_ciphers.FirstOrDefault(cipher => cipher.Handles(probeData.Algorithm)) is null
            || _derivations.FirstOrDefault(derivation => derivation.Handles(probeData)) is null)
            return new OdfDecryptionResult(OdfDecryptionStatus.Unsupported, null);

        var probeBytes = Read(archive, probe);
        byte[]? startKey = null;
        foreach (var candidate in source.Candidates(probeData))
        {
            if (Open(candidate, probeData, probeBytes) is not { } opened)
            {
                CryptographicOperations.ZeroMemory(candidate);
                continue;
            }

            CryptographicOperations.ZeroMemory(opened);
            startKey = candidate;
            break;
        }

        if (startKey is null) return new OdfDecryptionResult(source.Failure, null);
        var protection = source.ProtectionFor(startKey);

        if (probe == WrapperPath)
            return new OdfDecryptionResult(OdfDecryptionStatus.Decrypted, new MemoryStream(Open(startKey, probeData, probeBytes)!), protection);

        var output = new MemoryStream();
        long expanded = 0;
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in archive.Entries.OrderBy(entry => entry.FullName == "mimetype" ? 0 : 1))
            {
                if (entry.FullName.EndsWith('/')) continue;
                byte[] bytes;
                if (entry.FullName == ManifestPath) bytes = PlainManifest(manifest);
                else if (encrypted.TryGetValue(entry.FullName, out var data))
                    bytes = Open(startKey, data, Read(archive, entry.FullName)) ?? throw new InvalidDataException($"Cannot decrypt '{entry.FullName}'.");
                else bytes = Read(archive, entry.FullName);

                expanded += bytes.LongLength;
                if (expanded > MaxPackageBytes) throw new InvalidDataException("The decrypted package expands beyond the safe size limits.");
                var created = target.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Fastest);
                using var stored = created.Open();
                stored.Write(bytes);
            }
        }

        output.Position = 0;
        return new OdfDecryptionResult(OdfDecryptionStatus.Decrypted, output, protection);
    }

    public MemoryStream Encrypt(Stream package, OdfProtection protection)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(protection);
        if (protection is PasswordProtection { Password.Length: 0 }) throw new ArgumentException("The password is empty.", nameof(protection));
        var keying = protection.Keying();

        using var source = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = source.GetEntry(ManifestPath) ?? throw new InvalidDataException("The package has no manifest.");
        XDocument manifest;
        using (var input = manifestEntry.Open()) manifest = _xml.Load(input, LoadOptions.None);

        var cipher = new AesCbcCipher();
        var entries = new List<(string Path, byte[] Bytes, CompressionLevel Level)>();
        foreach (var entry in source.Entries.Where(entry => !entry.FullName.EndsWith('/') && entry.FullName != ManifestPath))
        {
            var plain = Read(source, entry.FullName);
            if (entry.FullName == "mimetype")
            {
                entries.Insert(0, (entry.FullName, plain, CompressionLevel.NoCompression));
                continue;
            }

            var compressed = Deflate(plain);
            var salt = keying.UsesSalt ? RandomNumberGenerator.GetBytes(16) : [];
            var iv = RandomNumberGenerator.GetBytes(16);
            entries.Add((entry.FullName, cipher.Encrypt(keying.KeyFor(salt), iv, compressed), CompressionLevel.NoCompression));
            Describe(manifest, entry.FullName, plain.LongLength, _checksum.Of(compressed), iv, keying.Describe(salt));
        }

        keying.Decorate(manifest.Root ?? throw new InvalidDataException("Invalid manifest."));

        var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, bytes, level) in entries)
            {
                using var stream = target.CreateEntry(path, level).Open();
                stream.Write(bytes);
            }

            using var manifestStream = target.CreateEntry(ManifestPath, CompressionLevel.Optimal).Open();
            manifest.Save(manifestStream, SaveOptions.DisableFormatting);
        }

        output.Position = 0;
        return output;
    }

    private void Describe(XDocument manifest, string path, long size, byte[] checksum, byte[] iv, IEnumerable<XElement> derivation)
    {
        var root = manifest.Root ?? throw new InvalidDataException("Invalid manifest.");
        var entry = root.Elements(Manifest + "file-entry").FirstOrDefault(element => (string?)element.Attribute(Manifest + "full-path") == path);
        if (entry is null)
        {
            entry = new XElement(Manifest + "file-entry",
                new XAttribute(Manifest + "full-path", path), new XAttribute(Manifest + "media-type", string.Empty));
            root.Add(entry);
        }

        entry.Elements(Manifest + "encryption-data").Remove();
        entry.SetAttributeValue(Manifest + "size", size);
        entry.Add(new XElement(Manifest + "encryption-data",
            new XAttribute(Manifest + "checksum-type", OdfCryptoNames.Sha256Checksum),
            new XAttribute(Manifest + "checksum", Convert.ToBase64String(checksum)),
            new XElement(Manifest + "algorithm",
                new XAttribute(Manifest + "algorithm-name", OdfCryptoNames.AesCbc),
                new XAttribute(Manifest + "initialisation-vector", Convert.ToBase64String(iv))),
            derivation));
    }

    private byte[]? Open(byte[] startKey, OdfEncryptionData data, byte[] encrypted)
    {
        var cipher = _ciphers.FirstOrDefault(candidate => candidate.Handles(data.Algorithm));
        var derivation = _derivations.FirstOrDefault(candidate => candidate.Handles(data));
        if (cipher is null || derivation is null) return null;

        byte[]? decrypted;
        try
        {
            decrypted = cipher.Decrypt(derivation.Derive(startKey, data), data.InitialisationVector, encrypted);
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidDataException)
        {
            return null;
        }

        if (decrypted is null || !_checksum.Matches(data, decrypted)) return null;
        return data.Size == decrypted.LongLength ? decrypted : Inflate(decrypted, data.Size);
    }

    private byte[]? Inflate(byte[] data, long? size)
    {
        using var input = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        try
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                if (output.Length > MaxEntryBytes) return null;
            }
        }
        catch (InvalidDataException)
        {
            return size is null ? data : null;
        }

        return size is { } expected && expected != output.Length ? null : output.ToArray();
    }

    private byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true)) deflate.Write(data);
        return output.ToArray();
    }

    private byte[] PlainManifest(XDocument manifest)
    {
        var copy = new XDocument(manifest);
        copy.Descendants(Manifest + "encryption-data").Remove();
        _openPgpKeys.Remove(copy.Root!);
        using var output = new MemoryStream();
        copy.Save(output, SaveOptions.DisableFormatting);
        return output.ToArray();
    }

    private byte[] Read(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidDataException($"Missing entry '{path}'.");
        if (entry.Length > MaxEntryBytes) throw new InvalidDataException("Entry is too large.");
        using var input = new BoundedReadStream(entry.Open(), MaxEntryBytes);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }
}

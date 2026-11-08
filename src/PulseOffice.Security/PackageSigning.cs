using System.Security.Cryptography;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace PulseOffice.Security;

public interface IDocumentSignatureService
{
    bool CanSign(string path);

    bool IsSigned(string path);

    IReadOnlyList<DocumentSignature> Read(string path);

    void Sign(string path, X509Certificate2 certificate);

    bool Remove(string path, string thumbprint);
}

public interface IPackageSignatureFormat : IDocumentSignatureService;

public sealed class DocumentSignatureService(IEnumerable<IPackageSignatureFormat> formats) : IDocumentSignatureService
{
    private readonly IReadOnlyList<IPackageSignatureFormat> _formats = [.. formats];

    public bool CanSign(string path) => For(path) is not null;

    public bool IsSigned(string path) => !string.IsNullOrWhiteSpace(path) && _formats.Any(format => format.IsSigned(path));

    public IReadOnlyList<DocumentSignature> Read(string path) => For(path)?.Read(path) ?? [];

    public void Sign(string path, X509Certificate2 certificate) =>
        (For(path) ?? throw new InvalidOperationException($"No signature format handles {Path.GetFileName(path)}."))
            .Sign(path, certificate);

    public bool Remove(string path, string thumbprint) => For(path)?.Remove(path, thumbprint) ?? false;

    private IPackageSignatureFormat? For(string path) =>
        string.IsNullOrWhiteSpace(path) ? null : _formats.FirstOrDefault(format => format.CanSign(path));
}

public sealed class OdfSignatureService(IXmlSignatureBuilder builder) : IPackageSignatureFormat
{
    public const string SignatureEntry = "META-INF/documentsignatures.xml";

    public const string MacroSignatureEntry = "META-INF/macrosignatures.xml";

    public bool CanSign(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".ods" or ".odt" or ".odp";

    public bool IsSigned(string path)
    {
        if (!File.Exists(path)) return false;

        try
        {
            using var archive = ZipFile.OpenRead(path);
            return archive.GetEntry(SignatureEntry) is not null || archive.GetEntry(MacroSignatureEntry) is not null;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public IReadOnlyList<DocumentSignature> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path)) return [];

        try
        {
            return ReadArchive(path);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
            or System.Xml.XmlException or FormatException or CryptographicException)
        {
            return [];
        }
    }

    private IReadOnlyList<DocumentSignature> ReadArchive(string path)
    {
        using var archive = ZipFile.OpenRead(path);

        var parts = new ArchiveParts(archive);
        var entry = archive.GetEntry(SignatureEntry);

        if (entry is null) return [];

        using var stream = entry.Open();

        XElement root;

        try
        {
            root = XElement.Load(stream, LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }

        return builder.Verify(root, parts);
    }

    public void Sign(string path, X509Certificate2 certificate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(certificate);

        XElement document;

        using (var archive = ZipFile.OpenRead(path))
        {
            var parts = new ArchiveParts(archive);
            var existing = Existing(archive);

            existing.Add(builder.Sign(parts, certificate, DateTimeOffset.Now));
            document = existing;
        }

        Write(path, document);
    }

    public bool Remove(string path, string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        XElement document;
        var removed = false;

        using (var archive = ZipFile.OpenRead(path))
        {
            document = Existing(archive);

            foreach (var element in document.Elements(XmlSignatureBuilder.Signature + "Signature").ToList())
            {
                var raw = element
                    .Descendants(XmlSignatureBuilder.Signature + "X509Certificate")
                    .Select(node => node.Value.Trim())
                    .FirstOrDefault();

                if (raw is null) continue;

                X509Certificate2 certificate;
                try
                {
                    certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(raw));
                }
                catch (Exception ex) when (ex is FormatException or CryptographicException)
                {
                    continue;
                }

                if (!string.Equals(certificate.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
                    continue;

                element.Remove();
                removed = true;
            }
        }

        if (removed) Write(path, document);

        return removed;
    }

    private static XElement Existing(ZipArchive archive)
    {
        if (archive.GetEntry(SignatureEntry) is { } entry)
        {
            using var stream = entry.Open();

            try
            {
                return XElement.Load(stream, LoadOptions.PreserveWhitespace);
            }
            catch (System.Xml.XmlException)
            {
            }
        }

        return new XElement(
            XmlSignatureBuilder.Document + "document-signatures",
            new XAttribute("version", "1.2"));
    }

    private static void Write(string path, XElement document)
    {
        var temporary = path + ".signing";

        try
        {
            WriteTemporary(path, temporary, document);
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }

    private static void WriteTemporary(string path, string temporary, XElement document)
    {
        using (var source = ZipFile.OpenRead(path))
        using (var target = new ZipArchive(File.Create(temporary), ZipArchiveMode.Create))
        {
            foreach (var entry in source.Entries)
            {
                if (string.Equals(entry.FullName, SignatureEntry, StringComparison.Ordinal)) continue;

                var level = string.Equals(entry.FullName, "mimetype", StringComparison.Ordinal)
                    ? CompressionLevel.NoCompression
                    : CompressionLevel.Optimal;

                var copy = target.CreateEntry(entry.FullName, level);

                using var from = entry.Open();
                using var to = copy.Open();

                from.CopyTo(to);
            }

            if (document.HasElements)
            {
                var signatures = target.CreateEntry(SignatureEntry, CompressionLevel.Optimal);

                using var stream = signatures.Open();
                document.Save(stream, SaveOptions.DisableFormatting);
            }
        }
    }

    private sealed class ArchiveParts(ZipArchive archive) : IPackageParts
    {
        private const long MaximumEntry = 512L * 1024 * 1024;

        public IReadOnlyList<string> Names => [.. archive.Entries.Select(entry => entry.FullName)];

        public byte[] Read(string name)
        {
            var entry = archive.GetEntry(name)
                ?? throw new InvalidOperationException($"The package has no entry named {name}.");

            if (entry.Length > MaximumEntry)
                throw new InvalidDataException($"The entry {name} is too large to verify.");

            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;

            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > MaximumEntry)
                    throw new InvalidDataException($"The entry {name} is too large to verify.");
                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
    }
}

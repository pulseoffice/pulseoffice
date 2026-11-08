using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Security;

public interface IPackageParts
{
    IReadOnlyList<string> Names { get; }

    byte[] Read(string name);
}

public interface IXmlSignatureBuilder
{
    XElement Sign(IPackageParts parts, X509Certificate2 certificate, DateTimeOffset signedOn);

    IReadOnlyList<DocumentSignature> Verify(XElement? signatures, IPackageParts parts);
}

public sealed class XmlSignatureBuilder(CertificateJudge judge) : IXmlSignatureBuilder
{
    public XmlSignatureBuilder() : this(new CertificateJudge())
    {
    }

    public static readonly XNamespace Signature = "http://www.w3.org/2000/09/xmldsig#";

    public static readonly XNamespace Document =
        "urn:oasis:names:tc:opendocument:xmlns:digitalsignature:1.0";

    public static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    private const string Canonicalisation = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315";
    private const string Method = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string Digest = "http://www.w3.org/2001/04/xmlenc#sha256";
    private const string ManifestEntry = "META-INF/manifest.xml";

    private static readonly XNamespace Manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
    private static readonly XNamespace Xades = "http://uri.etsi.org/01903/v1.3.2#";

    private readonly XmlDsigAlgorithms _algorithms = new();

    private XadesTimestampLocator Timestamps => new(_algorithms);

    public XElement Sign(IPackageParts parts, X509Certificate2 certificate, DateTimeOffset signedOn)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(certificate);

        if (certificate.GetRSAPrivateKey() is not { } key)
            throw new InvalidOperationException("The certificate has no usable private key.");

        var id = "ID_" + Guid.NewGuid().ToString("N");
        var propertyId = id + "_prop";

        var signedInfo = new XElement(
            Signature + "SignedInfo",
            new XElement(Signature + "CanonicalizationMethod", new XAttribute("Algorithm", Canonicalisation)),
            new XElement(Signature + "SignatureMethod", new XAttribute("Algorithm", Method)));

        var xml = XmlParts(parts);
        foreach (var name in Signable(parts))
        {
            var transformed = xml.Contains(name);
            var content = parts.Read(name);
            signedInfo.Add(Reference(Encode(name), Convert.ToBase64String(SHA256.HashData(transformed ? _algorithms.Canonical(content) : content)), transformed));
        }

        var property = new XElement(
            Signature + "SignatureProperty",
            new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName),
            new XAttribute("Id", propertyId),
            new XAttribute("Target", "#" + id),
            new XElement(Dc + "date", signedOn.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)));
        var properties = new XElement(Signature + "Object", new XElement(Signature + "SignatureProperties", property));

        var signature = new XElement(
            Signature + "Signature",
            new XAttribute("xmlns", Signature.NamespaceName),
            new XAttribute("Id", id),
            signedInfo,
            new XElement(Signature + "SignatureValue"),
            new XElement(
                Signature + "KeyInfo",
                new XElement(
                    Signature + "X509Data",
                    new XElement(Signature + "X509IssuerSerial",
                        new XElement(Signature + "X509IssuerName", certificate.Issuer),
                        new XElement(Signature + "X509SerialNumber", SerialOf(certificate))),
                    new XElement(Signature + "X509Certificate", Convert.ToBase64String(certificate.RawData)))),
            properties);

        var holder = new XElement(Document + "document-signatures", signature);
        signedInfo.Add(Reference("#" + propertyId, Convert.ToBase64String(SHA256.HashData(_algorithms.Canonical(property))), transformed: false));

        var value = key.SignData(_algorithms.Canonical(signedInfo), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        signature.Element(Signature + "SignatureValue")!.Value = Convert.ToBase64String(value);

        signature.Remove();
        return signature;
    }

    public IReadOnlyList<DocumentSignature> Verify(XElement? signatures, IPackageParts parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        if (signatures is null) return [];

        return [.. signatures.Elements(Signature + "Signature").Select(element => Read(element, parts)).OfType<DocumentSignature>()];
    }

    private DocumentSignature? Read(XElement element, IPackageParts parts)
    {
        if (element.Element(Signature + "SignedInfo") is not { } signedInfo) return null;

        var certificates = Certificates(element);
        if (certificates.Count == 0) return null;

        var signedOn = Date(element);
        var references = signedInfo.Elements(Signature + "Reference").ToList();
        var names = references
            .Select(reference => (string?)reference.Attribute("URI") ?? string.Empty)
            .Where(uri => !uri.StartsWith('#'))
            .Select(Uri.UnescapeDataString)
            .ToList();

        var signer = SignerIdentity.From(certificates[0]);
        var status = SignatureStatus.Broken;

        try
        {
            if (references.All(reference => Matches(reference, element, parts)) && Covered(names, parts))
                foreach (var certificate in certificates)
                {
                    if (!_algorithms.VerifySignature(signedInfo, element.Element(Signature + "SignatureValue")?.Value, certificate)) continue;
                    signer = SignerIdentity.From(certificate);
                    status = Unambiguous(element, certificate)
                        ? judge.Judge(certificate, Timestamps.Find(element))
                        : SignatureStatus.UntrustedCertificate;
                    break;
                }
        }
        catch (Exception ex) when (ex is CryptographicException or XmlException or InvalidOperationException
            or ArgumentException or FormatException or IOException or NotSupportedException)
        {
            status = SignatureStatus.Broken;
        }

        return new DocumentSignature(signer, signedOn, status, names);
    }

    private bool Matches(XElement reference, XElement signature, IPackageParts parts)
    {
        var uri = (string?)reference.Attribute("URI") ?? string.Empty;
        var expected = reference.Element(Signature + "DigestValue")?.Value.Trim();
        var algorithm = (string?)reference.Element(Signature + "DigestMethod")?.Attribute("Algorithm");
        var transforms = reference.Element(Signature + "Transforms")?.Elements(Signature + "Transform")
            .Select(transform => (string?)transform.Attribute("Algorithm") ?? string.Empty).ToList() ?? [];

        if (expected is null || transforms.Any(transform => !_algorithms.IsCanonicalisation(transform))) return false;

        byte[] content;
        if (uri.StartsWith('#'))
        {
            var target = signature.AncestorsAndSelf().Last();
            var matching = target.DescendantsAndSelf().Where(node => (string?)node.Attribute("Id") == uri[1..]).ToList();
            if (matching.Count != 1) return false;
            content = _algorithms.Canonical(matching[0]);
        }
        else
        {
            var name = Uri.UnescapeDataString(uri);
            if (!parts.Names.Contains(name)) return false;
            content = parts.Read(name);
            if (transforms.Count > 0) content = _algorithms.Canonical(content);
        }

        return _algorithms.Digest(algorithm, content) is { } actual && actual == expected;
    }

    private bool Unambiguous(XElement element, X509Certificate2 certificate)
    {
        var data = element.Element(Signature + "KeyInfo")?.Elements(Signature + "X509Data").ToList() ?? [];
        if (data.Count != 1) return false;

        var serial = data[0].Element(Signature + "X509IssuerSerial")?.Element(Signature + "X509SerialNumber")?.Value.Trim();
        return serial is null || serial == SerialOf(certificate);
    }

    private bool Covered(IReadOnlyList<string> names, IPackageParts parts)
    {
        if (parts.Names.Count() != parts.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count()) return false;

        var signed = names.ToHashSet(StringComparer.Ordinal);
        return parts.Names.Where(Signed)
            .Where(name => name == ManifestEntry || (name != "mimetype" && !name.StartsWith("META-INF/", StringComparison.Ordinal)))
            .All(signed.Contains);
    }

    private IReadOnlyList<X509Certificate2> Certificates(XElement element)
    {
        var certificates = new List<X509Certificate2>();
        foreach (var raw in element.Element(Signature + "KeyInfo")?.Descendants(Signature + "X509Certificate").Select(node => node.Value.Trim()) ?? [])
        {
            try
            {
                certificates.Add(X509CertificateLoader.LoadCertificate(Convert.FromBase64String(raw)));
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException)
            {
            }
        }

        return certificates;
    }

    private static DateTimeOffset Date(XElement element)
    {
        var referenced = element.Elements(Signature + "SignedInfo").Elements(Signature + "Reference")
            .Select(reference => (string?)reference.Attribute("URI"))
            .Where(uri => uri is { Length: > 1 } && uri[0] == '#')
            .Select(uri => uri![1..])
            .ToHashSet(StringComparer.Ordinal);

        var text = element.Descendants()
            .Where(node => referenced.Contains((string?)node.Attribute("Id") ?? string.Empty))
            .SelectMany(node => node.Descendants().Where(child => child.Name == Dc + "date" || child.Name == Xades + "SigningTime"))
            .FirstOrDefault()?.Value;

        return DateTimeOffset.TryParse(
            text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
    }

    public static IReadOnlyList<string> Signable(IPackageParts parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        return [.. parts.Names.Where(Signed).OrderBy(name => name, StringComparer.Ordinal)];
    }

    private static bool Signed(string name) =>
        !name.EndsWith('/')
        && name is not "META-INF/documentsignatures.xml" and not "META-INF/macrosignatures.xml" and not "META-INF/packagesignatures.xml";

    private static HashSet<string> XmlParts(IPackageParts parts)
    {
        var result = new HashSet<string>(StringComparer.Ordinal) { "META-INF/manifest.xml" };
        if (!parts.Names.Contains("META-INF/manifest.xml")) return result;

        try
        {
            var manifest = XDocument.Parse(System.Text.Encoding.UTF8.GetString(parts.Read("META-INF/manifest.xml")));
            foreach (var entry in manifest.Descendants(Manifest + "file-entry"))
                if ((string?)entry.Attribute(Manifest + "media-type") == "text/xml"
                    && (string?)entry.Attribute(Manifest + "full-path") is { } path)
                    result.Add(path);
        }
        catch (XmlException)
        {
        }

        return result;
    }

    private static XElement Reference(string uri, string digest, bool transformed) => new(
        Signature + "Reference",
        new XAttribute("URI", uri),
        transformed ? new XElement(Signature + "Transforms", new XElement(Signature + "Transform", new XAttribute("Algorithm", Canonicalisation))) : null,
        new XElement(Signature + "DigestMethod", new XAttribute("Algorithm", Digest)),
        new XElement(Signature + "DigestValue", digest));

    private static string SerialOf(X509Certificate2 certificate) =>
        System.Numerics.BigInteger.Parse("0" + certificate.SerialNumber, System.Globalization.NumberStyles.HexNumber).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Encode(string name) => string.Join('/', name.Split('/').Select(Uri.EscapeDataString));
}

public sealed class XmlDsigAlgorithms
{
    private static readonly XNamespace Signature = XmlSignatureBuilder.Signature;

    public bool IsCanonicalisation(string algorithm) =>
        algorithm is "http://www.w3.org/TR/2001/REC-xml-c14n-20010315"
            or "http://www.w3.org/TR/2001/REC-xml-c14n-20010315#WithComments"
            or "http://www.w3.org/2006/12/xml-c14n11";

    public string? Digest(string? algorithm, byte[] content) => algorithm switch
    {
        "http://www.w3.org/2000/09/xmldsig#sha1" => Convert.ToBase64String(SHA1.HashData(content)),
        "http://www.w3.org/2001/04/xmlenc#sha256" => Convert.ToBase64String(SHA256.HashData(content)),
        "http://www.w3.org/2001/04/xmldsig-more#sha384" => Convert.ToBase64String(SHA384.HashData(content)),
        "http://www.w3.org/2001/04/xmlenc#sha512" => Convert.ToBase64String(SHA512.HashData(content)),
        _ => null,
    };

    public bool VerifySignature(XElement signedInfo, string? value, X509Certificate2 certificate)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var method = (string?)signedInfo.Element(Signature + "SignatureMethod")?.Attribute("Algorithm") ?? string.Empty;
        var canonical = Canonical(signedInfo);
        var signature = Convert.FromBase64String(value.Trim());

        var hash = method switch
        {
            _ when method.EndsWith("sha1", StringComparison.Ordinal) => HashAlgorithmName.SHA1,
            _ when method.EndsWith("sha256", StringComparison.Ordinal) => HashAlgorithmName.SHA256,
            _ when method.EndsWith("sha384", StringComparison.Ordinal) => HashAlgorithmName.SHA384,
            _ when method.EndsWith("sha512", StringComparison.Ordinal) => HashAlgorithmName.SHA512,
            _ => default,
        };
        if (hash == default) return false;

        if (method.Contains("rsa-", StringComparison.Ordinal) && certificate.GetRSAPublicKey() is { } rsa)
            return rsa.VerifyData(canonical, signature, hash, RSASignaturePadding.Pkcs1);
        if (method.Contains("ecdsa-", StringComparison.Ordinal) && certificate.GetECDsaPublicKey() is { } ecdsa)
            return ecdsa.VerifyData(canonical, signature, hash);
        return false;
    }

    public byte[] Canonical(XElement element)
    {
        var copy = new XElement(element);
        for (var ancestor = element.Parent; ancestor is not null; ancestor = ancestor.Parent)
            foreach (var declaration in ancestor.Attributes().Where(attribute => attribute.IsNamespaceDeclaration))
                if (copy.Attribute(declaration.Name) is null) copy.Add(new XAttribute(declaration.Name, declaration.Value));

        return Canonical(System.Text.Encoding.UTF8.GetBytes(copy.ToString(SaveOptions.DisableFormatting)));
    }

    public byte[] Canonical(byte[] xml)
    {
        var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using (var reader = XmlReader.Create(new MemoryStream(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
            document.Load(reader);

        var transform = new XmlDsigC14NTransform();
        transform.LoadInput(document);

        using var stream = (Stream)transform.GetOutput(typeof(Stream));
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Security;

public sealed class OoxmlSignatureScope
{
    public const string OriginRelationship =
        "http://schemas.openxmlformats.org/package/2006/relationships/digital-signature/origin";

    public const string SignatureRelationship =
        "http://schemas.openxmlformats.org/package/2006/relationships/digital-signature/signature";

    public const string OriginPart = "_xmlsignatures/origin.sigs";

    public const string OriginRelationships = "_xmlsignatures/_rels/origin.sigs.rels";

    public const string OriginContentType = "application/vnd.openxmlformats-package.digital-signature-origin";

    public const string SignatureContentType =
        "application/vnd.openxmlformats-package.digital-signature-xmlsignature+xml";

    public const string RelationshipsContentType = "application/vnd.openxmlformats-package.relationships+xml";

    private const string SignatureFolder = "_xmlsignatures/";

    private readonly string[] _unsignedParts = ["docProps/app.xml", "docProps/core.xml"];

    private readonly string[] _unsignedRelationships =
    [
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties",
        "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties",
        OriginRelationship,
    ];

    public bool Signs(string part) =>
        !part.EndsWith('/')
        && part != OpcPackage.ContentTypesPart
        && !part.StartsWith(SignatureFolder, StringComparison.Ordinal)
        && !_unsignedParts.Contains(part, StringComparer.Ordinal);

    public bool MustBeSigned(string part) =>
        Signs(part) && !part.StartsWith("docProps/", StringComparison.Ordinal);

    public bool SignsRelationship(string type) => !_unsignedRelationships.Contains(type, StringComparer.Ordinal);

    public bool IsRelationships(string part, string? contentType) =>
        contentType == RelationshipsContentType || part.EndsWith(".rels", StringComparison.OrdinalIgnoreCase);
}

public interface IRelationshipTransform
{
    string Algorithm { get; }

    byte[] Apply(byte[] relationships, IReadOnlySet<string> sourceIds, IReadOnlySet<string> sourceTypes);
}

public sealed class RelationshipTransform(XmlDsigAlgorithms algorithms) : IRelationshipTransform
{
    public string Algorithm => "http://schemas.openxmlformats.org/package/2006/RelationshipTransform";

    public byte[] Apply(byte[] relationships, IReadOnlySet<string> sourceIds, IReadOnlySet<string> sourceTypes)
    {
        ArgumentNullException.ThrowIfNull(relationships);
        ArgumentNullException.ThrowIfNull(sourceIds);
        ArgumentNullException.ThrowIfNull(sourceTypes);

        XElement source;
        using (var reader = XmlReader.Create(
            new MemoryStream(relationships),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            source = XElement.Load(reader);
        }

        var ns = OpcPackage.Relationships;

        var chosen = source.Elements(ns + "Relationship")
            .Where(element => sourceIds.Contains((string?)element.Attribute("Id") ?? string.Empty)
                || sourceTypes.Contains((string?)element.Attribute("Type") ?? string.Empty))
            .OrderBy(element => (string?)element.Attribute("Id"), StringComparer.Ordinal)
            .Select(element =>
            {
                var copy = new XElement(
                    ns + "Relationship",
                    element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration).Select(attribute => new XAttribute(attribute)));

                if (copy.Attribute("TargetMode") is null) copy.Add(new XAttribute("TargetMode", "Internal"));

                return copy;
            });

        return algorithms.Canonical(new XElement(ns + "Relationships", new XAttribute("xmlns", ns.NamespaceName), chosen));
    }
}

public interface IOoxmlSignatureBuilder
{
    XElement Sign(IOpcParts parts, X509Certificate2 certificate, DateTimeOffset signedOn);

    DocumentSignature? Verify(XElement signature, IOpcParts parts);
}

public sealed class OoxmlSignatureBuilder(
    XmlDsigAlgorithms algorithms,
    IRelationshipTransform relationships,
    OoxmlSignatureScope scope,
    CertificateJudge judge) : IOoxmlSignatureBuilder
{
    private readonly XNamespace Ds = XmlSignatureBuilder.Signature;
    private readonly XNamespace Mdssi = "http://schemas.openxmlformats.org/package/2006/digital-signature";
    private readonly XNamespace Xades = "http://uri.etsi.org/01903/v1.3.2#";
    private readonly XNamespace OfficeDigsig = "http://schemas.microsoft.com/office/2006/digsig";

    private const string Canonicalisation = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315";
    private const string Sha256 = "http://www.w3.org/2001/04/xmlenc#sha256";
    private const string RsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string EcdsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256";
    private const string ObjectType = "http://www.w3.org/2000/09/xmldsig#Object";
    private const string SignedPropertiesType = "http://uri.etsi.org/01903#SignedProperties";

    private const string SignatureId = "idPackageSignature";
    private const string PackageObjectId = "idPackageObject";
    private const string OfficeObjectId = "idOfficeObject";
    private const string SignedPropertiesId = "idSignedProperties";

    public XElement Sign(IOpcParts parts, X509Certificate2 certificate, DateTimeOffset signedOn)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(certificate);

        var rsa = certificate.GetRSAPrivateKey();
        var ecdsa = rsa is null ? certificate.GetECDsaPrivateKey() : null;

        if (rsa is null && ecdsa is null)
            throw new InvalidOperationException("The certificate has no usable private key.");

        var time = signedOn.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        var packageObject = new XElement(
            Ds + "Object",
            new XAttribute("Id", PackageObjectId),
            new XElement(Ds + "Manifest", parts.Names.Where(scope.Signs).Order(StringComparer.Ordinal)
                .Select(name => ManifestReference(parts, name))
                .OfType<XElement>()),
            new XElement(
                Ds + "SignatureProperties",
                new XElement(
                    Ds + "SignatureProperty",
                    new XAttribute("Id", "idSignatureTime"),
                    new XAttribute("Target", "#" + SignatureId),
                    new XElement(
                        Mdssi + "SignatureTime",
                        new XAttribute(XNamespace.Xmlns + "mdssi", Mdssi.NamespaceName),
                        new XElement(Mdssi + "Format", "YYYY-MM-DDThh:mm:ssTZD"),
                        new XElement(Mdssi + "Value", time)))));

        var officeObject = new XElement(
            Ds + "Object",
            new XAttribute("Id", OfficeObjectId),
            new XElement(
                Ds + "SignatureProperties",
                new XElement(
                    Ds + "SignatureProperty",
                    new XAttribute("Id", "idOfficeV1Details"),
                    new XAttribute("Target", "#" + SignatureId),
                    SignatureInfo())));

        var signedProperties = new XElement(
            Xades + "SignedProperties",
            new XAttribute("Id", SignedPropertiesId),
            new XElement(
                Xades + "SignedSignatureProperties",
                new XElement(Xades + "SigningTime", time),
                new XElement(
                    Xades + "SigningCertificate",
                    new XElement(
                        Xades + "Cert",
                        new XElement(
                            Xades + "CertDigest",
                            new XElement(Ds + "DigestMethod", new XAttribute("Algorithm", Sha256)),
                            new XElement(Ds + "DigestValue", Convert.ToBase64String(SHA256.HashData(certificate.RawData)))),
                        new XElement(
                            Xades + "IssuerSerial",
                            new XElement(Ds + "X509IssuerName", certificate.Issuer),
                            new XElement(Ds + "X509SerialNumber", SerialOf(certificate))))),
                new XElement(
                    Xades + "SignaturePolicyIdentifier",
                    new XElement(Xades + "SignaturePolicyImplied"))));

        var qualifying = new XElement(
            Ds + "Object",
            new XElement(
                Xades + "QualifyingProperties",
                new XAttribute(XNamespace.Xmlns + "xd", Xades.NamespaceName),
                new XAttribute("Target", "#" + SignatureId),
                signedProperties));

        var signedInfo = new XElement(
            Ds + "SignedInfo",
            new XElement(Ds + "CanonicalizationMethod", new XAttribute("Algorithm", Canonicalisation)),
            new XElement(Ds + "SignatureMethod", new XAttribute("Algorithm", rsa is not null ? RsaSha256 : EcdsaSha256)));

        var signature = new XElement(
            Ds + "Signature",
            new XAttribute("xmlns", Ds.NamespaceName),
            new XAttribute("Id", SignatureId),
            signedInfo,
            new XElement(Ds + "SignatureValue"),
            new XElement(
                Ds + "KeyInfo",
                new XElement(Ds + "X509Data", new XElement(Ds + "X509Certificate", Convert.ToBase64String(certificate.RawData)))),
            packageObject,
            officeObject,
            qualifying);

        signedInfo.Add(
            SameDocumentReference(PackageObjectId, ObjectType, packageObject, transformed: false),
            SameDocumentReference(OfficeObjectId, ObjectType, officeObject, transformed: false),
            SameDocumentReference(SignedPropertiesId, SignedPropertiesType, signedProperties, transformed: true));

        var canonical = algorithms.Canonical(signedInfo);
        var value = rsa is not null
            ? rsa.SignData(canonical, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            : ecdsa!.SignData(canonical, HashAlgorithmName.SHA256);

        signature.Element(Ds + "SignatureValue")!.Value = Convert.ToBase64String(value);

        rsa?.Dispose();
        ecdsa?.Dispose();

        return signature;
    }

    public DocumentSignature? Verify(XElement signature, IOpcParts parts)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(parts);

        if (signature.Name != Ds + "Signature" || signature.Element(Ds + "SignedInfo") is not { } signedInfo) return null;

        var certificates = Certificates(signature);
        if (certificates.Count == 0) return null;

        var signedOn = Date(signature);
        var manifest = ManifestOf(signature, signedInfo);
        var names = manifest?.Elements(Ds + "Reference")
            .Select(reference => PartName((string?)reference.Attribute("URI") ?? string.Empty))
            .ToList() ?? [];

        var signer = SignerIdentity.From(certificates[0]);
        var status = SignatureStatus.Broken;

        try
        {
            if (manifest is not null
                && signedInfo.Elements(Ds + "Reference").All(reference => MatchesObject(reference, signature))
                && manifest.Elements(Ds + "Reference").All(reference => MatchesPart(reference, parts))
                && Covered(names, parts))
            {
                foreach (var certificate in certificates)
                {
                    if (!algorithms.VerifySignature(signedInfo, signature.Element(Ds + "SignatureValue")?.Value, certificate)) continue;

                    signer = SignerIdentity.From(certificate);
                    status = judge.Judge(certificate, new XadesTimestampLocator(algorithms).Find(signature));
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is CryptographicException or XmlException or InvalidOperationException
            or ArgumentException or FormatException or IOException or NotSupportedException)
        {
            status = SignatureStatus.Broken;
        }

        return new DocumentSignature(signer, signedOn, status, names);
    }

    private XElement? ManifestReference(IOpcParts parts, string name)
    {
        if (parts.ContentTypeOf(name) is not { } contentType) return null;

        var uri = "/" + string.Join('/', name.Split('/').Select(Uri.EscapeDataString)) + "?ContentType=" + contentType;
        var content = parts.Read(name);

        if (!scope.IsRelationships(name, contentType))
            return Reference(uri, null, null, Convert.ToBase64String(SHA256.HashData(content)));

        var ids = SignedRelationshipIds(content);
        var transforms = new XElement(
            Ds + "Transforms",
            new XElement(
                Ds + "Transform",
                new XAttribute("Algorithm", relationships.Algorithm),
                ids.Order(StringComparer.Ordinal).Select(id => new XElement(
                    Mdssi + "RelationshipReference",
                    new XAttribute(XNamespace.Xmlns + "mdssi", Mdssi.NamespaceName),
                    new XAttribute("SourceId", id)))),
            new XElement(Ds + "Transform", new XAttribute("Algorithm", Canonicalisation)));

        var digest = SHA256.HashData(relationships.Apply(content, ids, new HashSet<string>()));

        return Reference(uri, null, transforms, Convert.ToBase64String(digest));
    }

    private HashSet<string> SignedRelationshipIds(byte[] content)
    {
        using var reader = XmlReader.Create(
            new MemoryStream(content),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

        return XElement.Load(reader).Elements(OpcPackage.Relationships + "Relationship")
            .Where(element => scope.SignsRelationship((string?)element.Attribute("Type") ?? string.Empty))
            .Select(element => (string?)element.Attribute("Id"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    private XElement SameDocumentReference(string id, string type, XElement target, bool transformed) =>
        Reference(
            "#" + id,
            type,
            transformed ? new XElement(Ds + "Transforms", new XElement(Ds + "Transform", new XAttribute("Algorithm", Canonicalisation))) : null,
            Convert.ToBase64String(SHA256.HashData(algorithms.Canonical(target))));

    private XElement Reference(string uri, string? type, XElement? transforms, string digest) => new(
        Ds + "Reference",
        type is null ? null : new XAttribute("Type", type),
        new XAttribute("URI", uri),
        transforms,
        new XElement(Ds + "DigestMethod", new XAttribute("Algorithm", Sha256)),
        new XElement(Ds + "DigestValue", digest));

    private XElement SignatureInfo() => new(
        OfficeDigsig + "SignatureInfoV1",
        new XAttribute("xmlns", OfficeDigsig.NamespaceName),
        new XElement(OfficeDigsig + "SetupID"),
        new XElement(OfficeDigsig + "SignatureText"),
        new XElement(OfficeDigsig + "SignatureImage"),
        new XElement(OfficeDigsig + "SignatureComments"),
        new XElement(OfficeDigsig + "WindowsVersion", "6.1"),
        new XElement(OfficeDigsig + "OfficeVersion", "16.0"),
        new XElement(OfficeDigsig + "ApplicationVersion", "16.0"),
        new XElement(OfficeDigsig + "Monitors", "1"),
        new XElement(OfficeDigsig + "HorizontalResolution", "1280"),
        new XElement(OfficeDigsig + "VerticalResolution", "800"),
        new XElement(OfficeDigsig + "ColorDepth", "32"),
        new XElement(OfficeDigsig + "SignatureProviderId", "{00000000-0000-0000-0000-000000000000}"),
        new XElement(OfficeDigsig + "SignatureProviderUrl"),
        new XElement(OfficeDigsig + "SignatureProviderDetails", "9"),
        new XElement(OfficeDigsig + "SignatureType", "1"));

    private XElement? ManifestOf(XElement signature, XElement signedInfo)
    {
        var referenced = signedInfo.Elements(Ds + "Reference")
            .Select(reference => (string?)reference.Attribute("URI") ?? string.Empty)
            .Where(uri => uri.StartsWith('#'))
            .Select(uri => uri[1..])
            .ToHashSet(StringComparer.Ordinal);

        var manifests = signature.Elements(Ds + "Object")
            .Where(element => referenced.Contains((string?)element.Attribute("Id") ?? string.Empty))
            .Elements(Ds + "Manifest")
            .ToList();

        return manifests.Count == 1 ? manifests[0] : null;
    }

    private bool MatchesObject(XElement reference, XElement signature)
    {
        var uri = (string?)reference.Attribute("URI") ?? string.Empty;
        if (!uri.StartsWith('#')) return false;

        var transforms = TransformsOf(reference);
        if (transforms.Any(transform => !algorithms.IsCanonicalisation((string?)transform.Attribute("Algorithm") ?? string.Empty)))
            return false;

        var matching = signature.DescendantsAndSelf().Where(node => (string?)node.Attribute("Id") == uri[1..]).ToList();
        if (matching.Count != 1) return false;

        return Digested(reference, algorithms.Canonical(matching[0]));
    }

    private bool MatchesPart(XElement reference, IOpcParts parts)
    {
        var uri = (string?)reference.Attribute("URI") ?? string.Empty;
        var name = PartName(uri);
        if (!parts.Names.Contains(name, StringComparer.Ordinal)) return false;
        if (!string.Equals(DeclaredContentType(uri), parts.ContentTypeOf(name), StringComparison.OrdinalIgnoreCase)) return false;

        var content = parts.Read(name);
        var transforms = TransformsOf(reference);

        foreach (var transform in transforms)
        {
            var algorithm = (string?)transform.Attribute("Algorithm") ?? string.Empty;

            if (algorithm == relationships.Algorithm)
            {
                var ids = transform.Elements(Mdssi + "RelationshipReference")
                    .Select(element => (string?)element.Attribute("SourceId")).OfType<string>().ToHashSet(StringComparer.Ordinal);
                var types = transform.Elements(Mdssi + "RelationshipsGroupReference")
                    .Select(element => (string?)element.Attribute("SourceType")).OfType<string>().ToHashSet(StringComparer.Ordinal);

                content = relationships.Apply(content, ids, types);
            }
            else if (algorithms.IsCanonicalisation(algorithm))
            {
                content = algorithms.Canonical(content);
            }
            else
            {
                return false;
            }
        }

        return Digested(reference, content);
    }

    private bool Digested(XElement reference, byte[] content)
    {
        var expected = reference.Element(Ds + "DigestValue")?.Value.Trim();
        var method = (string?)reference.Element(Ds + "DigestMethod")?.Attribute("Algorithm");

        return expected is not null && algorithms.Digest(method, content) is { } actual && actual == expected;
    }

    private IReadOnlyList<XElement> TransformsOf(XElement reference) =>
        [.. reference.Element(Ds + "Transforms")?.Elements(Ds + "Transform") ?? []];

    private bool Covered(IReadOnlyList<string> names, IOpcParts parts)
    {
        if (parts.Names.Count != parts.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count()) return false;

        var signed = names.ToHashSet(StringComparer.Ordinal);

        return parts.Names.Where(scope.MustBeSigned).All(signed.Contains);
    }

    private string PartName(string uri)
    {
        var query = uri.IndexOf('?', StringComparison.Ordinal);
        var path = query >= 0 ? uri[..query] : uri;

        return Uri.UnescapeDataString(path.TrimStart('/'));
    }

    private string? DeclaredContentType(string uri)
    {
        const string marker = "ContentType=";
        var query = uri.IndexOf('?', StringComparison.Ordinal);
        if (query < 0) return null;

        var parameter = uri[(query + 1)..];
        return parameter.StartsWith(marker, StringComparison.Ordinal) ? Uri.UnescapeDataString(parameter[marker.Length..]) : null;
    }

    private IReadOnlyList<X509Certificate2> Certificates(XElement signature)
    {
        var certificates = new List<X509Certificate2>();

        foreach (var raw in signature.Element(Ds + "KeyInfo")?.Descendants(Ds + "X509Certificate").Select(node => node.Value.Trim()) ?? [])
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

    private DateTimeOffset Date(XElement signature)
    {
        var referenced = signature.Element(Ds + "SignedInfo")?.Elements(Ds + "Reference")
            .Select(reference => (string?)reference.Attribute("URI") ?? string.Empty)
            .Where(uri => uri.Length > 1 && uri[0] == '#')
            .Select(uri => uri[1..])
            .ToHashSet(StringComparer.Ordinal) ?? [];
        var signed = signature.Descendants()
            .Where(node => referenced.Contains((string?)node.Attribute("Id") ?? string.Empty))
            .SelectMany(node => node.DescendantsAndSelf())
            .ToList();
        var text = signed.Where(node => node.Name == Xades + "SigningTime").Select(node => node.Value).FirstOrDefault()
            ?? signed.Where(node => node.Name == Mdssi + "Value").Select(node => node.Value).FirstOrDefault();

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
    }

    private string SerialOf(X509Certificate2 certificate) =>
        System.Numerics.BigInteger.Parse("0" + certificate.SerialNumber, NumberStyles.HexNumber).ToString(CultureInfo.InvariantCulture);
}

public sealed class OoxmlSignatureService(
    IOoxmlSignatureBuilder builder,
    IOpcPackageStore store) : IPackageSignatureFormat
{
    private const string SignatureFolder = "_xmlsignatures/";

    public bool CanSign(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".docx" or ".docm" or ".xlsx" or ".xlsm" or ".pptx" or ".pptm";

    public bool IsSigned(string path)
    {
        if (!CanSign(path) || !File.Exists(path)) return false;

        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(path);
            return archive.Entries.Any(entry => entry.FullName.StartsWith(SignatureFolder, StringComparison.Ordinal)
                && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
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
            var package = store.Load(path);

            return [.. SignatureParts(package)
                .Select(part => Verify(package, part))
                .OfType<DocumentSignature>()];
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
            or XmlException or FormatException or CryptographicException or InvalidOperationException)
        {
            return [];
        }
    }

    public void Sign(string path, X509Certificate2 certificate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(certificate);

        var package = store.Load(path);

        if (SignatureParts(package).Count == 0) CompactRelationships(package);
        EnsureOrigin(package);

        var part = NextSignaturePart(package);
        var signature = builder.Sign(package, certificate, DateTimeOffset.Now);

        package.WriteXml(part, signature);
        AddSignatureRelationship(package, part);
        Override(package, part, OoxmlSignatureScope.SignatureContentType);

        store.Save(package, path);
    }

    public bool Remove(string path, string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var package = store.Load(path);
        var removed = false;

        foreach (var part in SignatureParts(package))
        {
            if (!SignedBy(package, part, thumbprint)) continue;

            package.Remove(part);
            RemoveSignatureRelationship(package, part);
            RemoveOverride(package, part);
            removed = true;
        }

        if (!removed) return false;

        if (SignatureParts(package).Count == 0) RemoveOrigin(package);

        store.Save(package, path);
        return true;
    }

    private DocumentSignature? Verify(OpcPackage package, string part)
    {
        try
        {
            return builder.Verify(package.ReadXml(part), package);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private bool SignedBy(OpcPackage package, string part, string thumbprint)
    {
        try
        {
            var raw = package.ReadXml(part)
                .Descendants(XmlSignatureBuilder.Signature + "X509Certificate")
                .Select(node => node.Value.Trim())
                .FirstOrDefault();

            if (raw is null) return false;

            using var certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(raw));
            return string.Equals(certificate.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is XmlException or FormatException or CryptographicException)
        {
            return false;
        }
    }

    private IReadOnlyList<string> SignatureParts(OpcPackage package)
    {
        if (!package.Contains(OoxmlSignatureScope.OriginRelationships)) return [];

        return [.. package.ReadXml(OoxmlSignatureScope.OriginRelationships)
            .Elements(OpcPackage.Relationships + "Relationship")
            .Where(element => (string?)element.Attribute("Type") == OoxmlSignatureScope.SignatureRelationship)
            .Select(element => (string?)element.Attribute("Target"))
            .OfType<string>()
            .Select(target => package.Resolve(OoxmlSignatureScope.OriginPart, target))
            .Where(package.Contains)
            .Distinct(StringComparer.Ordinal)];
    }

    private string NextSignaturePart(OpcPackage package)
    {
        for (var index = 1; ; index++)
        {
            var candidate = $"{SignatureFolder}sig{index}.xml";
            if (!package.Contains(candidate)) return candidate;
        }
    }

    private void CompactRelationships(OpcPackage package)
    {
        foreach (var part in package.Names.Where(name => name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var root = package.ReadXml(part);
            var blanks = root.DescendantNodes().OfType<XText>().Where(text => string.IsNullOrWhiteSpace(text.Value)).ToList();
            if (blanks.Count == 0) continue;

            blanks.ForEach(text => text.Remove());
            package.WriteXml(part, root);
        }
    }

    private void EnsureOrigin(OpcPackage package)
    {
        if (!package.Contains(OoxmlSignatureScope.OriginPart)) package.Write(OoxmlSignatureScope.OriginPart, []);

        var types = Types(package);
        if (!types.Elements(OpcPackage.ContentTypes + "Default")
            .Any(element => string.Equals((string?)element.Attribute("Extension"), "sigs", StringComparison.OrdinalIgnoreCase)))
        {
            types.AddFirst(new XElement(
                OpcPackage.ContentTypes + "Default",
                new XAttribute("Extension", "sigs"),
                new XAttribute("ContentType", OoxmlSignatureScope.OriginContentType)));
            package.WriteXml(OpcPackage.ContentTypesPart, types);
        }

        var root = Relationships(package, "_rels/.rels");
        if (root.Elements(OpcPackage.Relationships + "Relationship")
            .Any(element => (string?)element.Attribute("Type") == OoxmlSignatureScope.OriginRelationship))
            return;

        root.Add(Relationship(FreeId(root), OoxmlSignatureScope.OriginRelationship, OoxmlSignatureScope.OriginPart));
        package.WriteXml("_rels/.rels", root);
    }

    private void RemoveOrigin(OpcPackage package)
    {
        package.Remove(OoxmlSignatureScope.OriginPart);
        package.Remove(OoxmlSignatureScope.OriginRelationships);

        if (package.Contains("_rels/.rels"))
        {
            var root = package.ReadXml("_rels/.rels");
            root.Elements(OpcPackage.Relationships + "Relationship")
                .Where(element => (string?)element.Attribute("Type") == OoxmlSignatureScope.OriginRelationship)
                .Remove();
            package.WriteXml("_rels/.rels", root);
        }

        var types = Types(package);
        types.Elements(OpcPackage.ContentTypes + "Default")
            .Where(element => string.Equals((string?)element.Attribute("Extension"), "sigs", StringComparison.OrdinalIgnoreCase))
            .Remove();
        package.WriteXml(OpcPackage.ContentTypesPart, types);
    }

    private void AddSignatureRelationship(OpcPackage package, string part)
    {
        var relationships = Relationships(package, OoxmlSignatureScope.OriginRelationships);
        relationships.Add(Relationship(FreeId(relationships), OoxmlSignatureScope.SignatureRelationship, Path.GetFileName(part)));
        package.WriteXml(OoxmlSignatureScope.OriginRelationships, relationships);
    }

    private void RemoveSignatureRelationship(OpcPackage package, string part)
    {
        var relationships = Relationships(package, OoxmlSignatureScope.OriginRelationships);
        relationships.Elements(OpcPackage.Relationships + "Relationship")
            .Where(element => (string?)element.Attribute("Target") is { } target
                && package.Resolve(OoxmlSignatureScope.OriginPart, target) == part)
            .Remove();
        package.WriteXml(OoxmlSignatureScope.OriginRelationships, relationships);
    }

    private void Override(OpcPackage package, string part, string contentType)
    {
        var types = Types(package);
        types.Add(new XElement(
            OpcPackage.ContentTypes + "Override",
            new XAttribute("PartName", "/" + part),
            new XAttribute("ContentType", contentType)));
        package.WriteXml(OpcPackage.ContentTypesPart, types);
    }

    private void RemoveOverride(OpcPackage package, string part)
    {
        var types = Types(package);
        types.Elements(OpcPackage.ContentTypes + "Override")
            .Where(element => string.Equals((string?)element.Attribute("PartName"), "/" + part, StringComparison.OrdinalIgnoreCase))
            .Remove();
        package.WriteXml(OpcPackage.ContentTypesPart, types);
    }

    private XElement Types(OpcPackage package) =>
        package.Contains(OpcPackage.ContentTypesPart)
            ? package.ReadXml(OpcPackage.ContentTypesPart)
            : throw new InvalidDataException("The package has no content types.");

    private XElement Relationships(OpcPackage package, string part) =>
        package.Contains(part)
            ? package.ReadXml(part)
            : new XElement(OpcPackage.Relationships + "Relationships", new XAttribute("xmlns", OpcPackage.Relationships.NamespaceName));

    private XElement Relationship(string id, string type, string target) => new(
        OpcPackage.Relationships + "Relationship",
        new XAttribute("Id", id),
        new XAttribute("Type", type),
        new XAttribute("Target", target));

    private string FreeId(XElement relationships)
    {
        var used = relationships.Elements(OpcPackage.Relationships + "Relationship")
            .Select(element => (string?)element.Attribute("Id"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        for (var index = 1; ; index++)
        {
            var candidate = "rId" + index.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(candidate)) return candidate;
        }
    }
}

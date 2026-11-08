using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Security;
using Xunit;

namespace PulseOffice.Security.Tests;

public sealed class OoxmlSignatureServiceTests
{
    private readonly XmlDsigAlgorithms _algorithms = new();
    private readonly OoxmlSignatureService _service;

    public OoxmlSignatureServiceTests()
    {
        _service = new OoxmlSignatureService(
            new OoxmlSignatureBuilder(_algorithms, new RelationshipTransform(_algorithms), new OoxmlSignatureScope(), new CertificateJudge()),
            new OpcPackageStore());
    }

    private static X509Certificate2 Certificate(string name = "CN=Firmatario")
    {
        using var key = RSA.Create(2048);

        return new CertificateRequest(name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(365));
    }

    private static string Package()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".docx");

        using var archive = new ZipArchive(File.Create(path), ZipArchiveMode.Create);

        void Add(string name, string content)
        {
            using var stream = archive.CreateEntry(name).Open();
            stream.Write(Encoding.UTF8.GetBytes(content));
        }

        Add("[Content_Types].xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
            + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
            + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
            + "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>"
            + "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>"
            + "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>"
            + "</Types>");
        Add("_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n"
            + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n"
            + "  <Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>\n"
            + "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>\n"
            + "</Relationships>");
        Add("word/_rels/document.xml.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
            + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
            + "</Relationships>");
        Add("word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Ciao</w:t></w:r></w:p></w:body></w:document>");
        Add("word/styles.xml", "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"/>");
        Add("docProps/core.xml", "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\"/>");

        return path;
    }

    private static void Replace(string path, string name, string content)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        archive.GetEntry(name)?.Delete();

        using var stream = archive.CreateEntry(name).Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
    }

    private static string Entry(string path, string name)
    {
        using var archive = ZipFile.OpenRead(path);
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void UnDocumentoFirmatoRipresentaLaPropriaFirma()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();

            Assert.True(_service.CanSign(path));
            Assert.False(_service.IsSigned(path));

            _service.Sign(path, certificate);

            var signature = Assert.Single(_service.Read(path));

            Assert.Equal("Firmatario", signature.Signer.Name);
            Assert.NotEqual(SignatureStatus.Broken, signature.Status);
            Assert.Contains("word/document.xml", signature.SignedParts);
            Assert.Contains("word/_rels/document.xml.rels", signature.SignedParts);
            Assert.Contains("_rels/.rels", signature.SignedParts);
            Assert.DoesNotContain("docProps/core.xml", signature.SignedParts);
            Assert.True(_service.IsSigned(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LeRelazioniSiCompattanoPrimaDellaFirmaPerchéUnAltraSuiteContaGliSpazi()
    {
        var path = Package();

        try
        {
            Replace(path, "word/_rels/document.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n  "
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>\n</Relationships>");

            using var certificate = Certificate();
            _service.Sign(path, certificate);

            Assert.DoesNotContain("\n", Entry(path, "_rels/.rels").Split("?>", 2)[1], StringComparison.Ordinal);
            Assert.DoesNotContain("\n", Entry(path, "word/_rels/document.xml.rels").Split("?>", 2)[1], StringComparison.Ordinal);
            Assert.NotEqual(SignatureStatus.Broken, Assert.Single(_service.Read(path)).Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LaSecondaFirmaNonRiscriveLeRelazioniGiàFirmate()
    {
        var path = Package();

        try
        {
            using var first = Certificate("CN=Primo");
            using var second = Certificate("CN=Secondo");
            _service.Sign(path, first);
            var signed = Entry(path, "word/_rels/document.xml.rels");

            _service.Sign(path, second);

            Assert.Equal(signed, Entry(path, "word/_rels/document.xml.rels"));
            Assert.All(_service.Read(path), signature => Assert.NotEqual(SignatureStatus.Broken, signature.Status));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IlPacchettoDichiaraOrigineFirmaETipi()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();
            _service.Sign(path, certificate);

            var types = Entry(path, "[Content_Types].xml");
            Assert.Contains("Extension=\"sigs\" ContentType=\"application/vnd.openxmlformats-package.digital-signature-origin\"", types, StringComparison.Ordinal);
            Assert.Contains("PartName=\"/_xmlsignatures/sig1.xml\" ContentType=\"application/vnd.openxmlformats-package.digital-signature-xmlsignature+xml\"", types, StringComparison.Ordinal);

            Assert.Contains(OoxmlSignatureScope.OriginRelationship, Entry(path, "_rels/.rels"), StringComparison.Ordinal);
            Assert.Contains(OoxmlSignatureScope.SignatureRelationship, Entry(path, "_xmlsignatures/_rels/origin.sigs.rels"), StringComparison.Ordinal);

            var signature = XElement.Parse(Entry(path, "_xmlsignatures/sig1.xml"));
            XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";
            Assert.Contains(signature.Elements(ds + "Object"), element => (string?)element.Attribute("Id") == "idPackageObject");
            Assert.Contains(signature.Elements(ds + "Object"), element => (string?)element.Attribute("Id") == "idOfficeObject");
            Assert.Contains(signature.Descendants(), element => element.Name.LocalName == "SignatureInfoV1");
            Assert.Contains(signature.Descendants(), element => element.Name.LocalName == "SigningCertificate");
            Assert.Contains(signature.Descendants(), element => element.Name.LocalName == "RelationshipReference");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LaFirmaXmlSiVerificaAncheConSignedXml()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();
            _service.Sign(path, certificate);

            var document = new System.Xml.XmlDocument { PreserveWhitespace = true };
            document.LoadXml(Entry(path, "_xmlsignatures/sig1.xml"));

            var signed = new System.Security.Cryptography.Xml.SignedXml(document);
            signed.LoadXml(document.DocumentElement!);

            Assert.True(signed.CheckSignature(certificate, verifySignatureOnly: true));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnaDataAggiuntaFuoriDalleProprietaFirmateNonCambiaLaDataDellaFirma()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();
            _service.Sign(path, certificate);
            var original = Assert.Single(_service.Read(path)).SignedOn;

            var xml = Entry(path, "_xmlsignatures/sig1.xml");
            var withObject = xml.Insert(xml.IndexOf("</SignedInfo>", StringComparison.Ordinal) + "</SignedInfo>".Length,
                "<Object><xd:SigningTime xmlns:xd=\"http://uri.etsi.org/01903/v1.3.2#\">2001-01-01T00:00:00Z</xd:SigningTime></Object>");
            Replace(path, "_xmlsignatures/sig1.xml", withObject);

            var read = Assert.Single(_service.Read(path));
            Assert.Equal(original, read.SignedOn);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ModificareUnaParteRompeLaFirma()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();
            _service.Sign(path, certificate);

            Replace(path, "word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>");

            Assert.Equal(SignatureStatus.Broken, Assert.Single(_service.Read(path)).Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AggiungereUnaParteRompeLaFirma()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();
            _service.Sign(path, certificate);

            Replace(path, "word/media/intrusa.xml", "<x/>");

            Assert.Equal(SignatureStatus.Broken, Assert.Single(_service.Read(path)).Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RiscrivereLeRelazioniSenzaCambiarleNonRompeLaFirma()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();
            _service.Sign(path, certificate);

            var rels = XElement.Parse(Entry(path, "word/_rels/document.xml.rels"));
            Replace(path, "word/_rels/document.xml.rels", rels.ToString(SaveOptions.None));
            Replace(path, "docProps/core.xml", "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\">altro</cp:coreProperties>");

            Assert.NotEqual(SignatureStatus.Broken, Assert.Single(_service.Read(path)).Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CambiareUnaRelazioneFirmataRompeLaFirma()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();
            _service.Sign(path, certificate);

            Replace(path, "word/_rels/document.xml.rels", Entry(path, "word/_rels/document.xml.rels").Replace("styles.xml", "altro.xml", StringComparison.Ordinal));

            Assert.Equal(SignatureStatus.Broken, Assert.Single(_service.Read(path)).Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DueFirmeConvivonoESiTolgonoUnaAllaVolta()
    {
        var path = Package();

        try
        {
            using var first = Certificate("CN=Prima");
            using var second = Certificate("CN=Seconda");

            _service.Sign(path, first);
            _service.Sign(path, second);

            var signatures = _service.Read(path);
            Assert.Equal(["Prima", "Seconda"], signatures.Select(signature => signature.Signer.Name));
            Assert.All(signatures, signature => Assert.NotEqual(SignatureStatus.Broken, signature.Status));

            Assert.True(_service.Remove(path, first.Thumbprint));
            Assert.Equal("Seconda", Assert.Single(_service.Read(path)).Signer.Name);
            Assert.NotEqual(SignatureStatus.Broken, Assert.Single(_service.Read(path)).Status);

            Assert.True(_service.Remove(path, second.Thumbprint));
            Assert.Empty(_service.Read(path));
            Assert.False(_service.IsSigned(path));
            Assert.DoesNotContain(OoxmlSignatureScope.OriginRelationship, Entry(path, "_rels/.rels"), StringComparison.Ordinal);
            Assert.DoesNotContain("sigs", Entry(path, "[Content_Types].xml"), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LaTrasformazioneDelleRelazioniOrdinaFiltraECompleta()
    {
        var transform = new RelationshipTransform(_algorithms);
        var source = Encoding.UTF8.GetBytes(
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n"
            + "  <Relationship Type=\"t2\" Target=\"b.xml\" Id=\"rId2\"/>\n"
            + "  <Relationship Type=\"t1\" Target=\"a.xml\" Id=\"rId1\"/>\n"
            + "  <Relationship Type=\"t3\" Target=\"http://x\" TargetMode=\"External\" Id=\"rId3\"/>\n"
            + "</Relationships>");

        var output = Encoding.UTF8.GetString(transform.Apply(source, new HashSet<string> { "rId2", "rId1" }, new HashSet<string>()));

        Assert.Equal(
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
            + "<Relationship Id=\"rId1\" Target=\"a.xml\" TargetMode=\"Internal\" Type=\"t1\"></Relationship>"
            + "<Relationship Id=\"rId2\" Target=\"b.xml\" TargetMode=\"Internal\" Type=\"t2\"></Relationship>"
            + "</Relationships>",
            output);
    }

    [Fact]
    public void IlServizioUnicoSceglieIlFormatoDalNome()
    {
        var composite = new DocumentSignatureService([new OdfSignatureService(new XmlSignatureBuilder()), _service]);

        Assert.True(composite.CanSign("a.docx"));
        Assert.True(composite.CanSign("a.xlsx"));
        Assert.True(composite.CanSign("a.pptx"));
        Assert.True(composite.CanSign("a.ods"));
        Assert.False(composite.CanSign("a.pdf"));
    }

    [Fact]
    public void SostituireIlCertificatoNellaFirmaLaRompe()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();
            using var impostor = Certificate("CN=Banca");
            _service.Sign(path, certificate);

            string signaturePart;
            using (var archive = ZipFile.OpenRead(path))
                signaturePart = archive.Entries.Select(entry => entry.FullName).First(name => name.StartsWith("_xmlsignatures/sig", StringComparison.Ordinal) && name.EndsWith(".xml", StringComparison.Ordinal));
            var forged = System.Text.RegularExpressions.Regex.Replace(Entry(path, signaturePart), "<X509Certificate>[^<]*</X509Certificate>", $"<X509Certificate>{Convert.ToBase64String(impostor.RawData)}</X509Certificate>");
            Replace(path, signaturePart, forged);

            var signature = Assert.Single(_service.Read(path));
            Assert.Equal(SignatureStatus.Broken, signature.Status);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

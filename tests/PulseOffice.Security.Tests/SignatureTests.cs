using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Security;
using Xunit;

namespace PulseOffice.Security.Tests;

public sealed class Parts(Dictionary<string, byte[]> entries) : IPackageParts
{
    public IReadOnlyList<string> Names => [.. entries.Keys];

    public byte[] Read(string name) => entries[name];

    public void Replace(string name, string content) => entries[name] = Encoding.UTF8.GetBytes(content);

    public static Parts Sample() => new(new Dictionary<string, byte[]>(StringComparer.Ordinal)
    {
        ["mimetype"] = Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.spreadsheet"),
        ["content.xml"] = Encoding.UTF8.GetBytes("<office:document-content/>"),
        ["styles.xml"] = Encoding.UTF8.GetBytes("<office:document-styles/>"),
        ["META-INF/manifest.xml"] = Encoding.UTF8.GetBytes("<manifest/>"),
    });
}

public sealed class XmlSignatureBuilderTests
{
    private readonly XmlSignatureBuilder _builder = new();

    private static X509Certificate2 Certificate(string name = "CN=Prova", int days = 365)
    {
        using var key = RSA.Create(2048);

        var request = new CertificateRequest(name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(days));
    }

    private XElement Signed(Parts parts, X509Certificate2 certificate) =>
        new(
            XmlSignatureBuilder.Document + "document-signatures",
            _builder.Sign(parts, certificate, DateTimeOffset.Now));

    [Fact]
    public void LaFirmaCopreTutteLeVociTranneLeFirmeStesse()
    {
        var parts = Parts.Sample();
        using var certificate = Certificate();

        var signature = Assert.Single(_builder.Verify(Signed(parts, certificate), parts));

        Assert.Contains("content.xml", signature.SignedParts);
        Assert.Contains("styles.xml", signature.SignedParts);
        Assert.Contains("mimetype", signature.SignedParts);
        Assert.Contains("META-INF/manifest.xml", signature.SignedParts);
        Assert.DoesNotContain("META-INF/documentsignatures.xml", signature.SignedParts);
    }

    [Fact]
    public void UnaFirmaAppenaFattaSiVerifica()
    {
        var parts = Parts.Sample();
        using var certificate = Certificate();

        var signature = Assert.Single(_builder.Verify(Signed(parts, certificate), parts));

        Assert.Equal("Prova", signature.Signer.Name);
        Assert.NotEqual(SignatureStatus.Broken, signature.Status);
    }

    [Fact]
    public void UnDocumentoConMacroBasicSiFirmaNonostanteLaDichiarazioneDelTipo()
    {
        var parts = Parts.Sample();
        parts.Replace(
            "Basic/Standard/Module1.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE script:module PUBLIC \"-//OpenOffice.org//DTD OfficeDocument 1.0//EN\" \"module.dtd\">\n<script:module xmlns:script=\"http://openoffice.org/2000/script\" script:name=\"Module1\" script:language=\"StarBasic\">Sub Main\nEnd Sub</script:module>");
        using var certificate = Certificate();

        var signature = Assert.Single(_builder.Verify(Signed(parts, certificate), parts));

        Assert.Contains("Basic/Standard/Module1.xml", signature.SignedParts);
        Assert.NotEqual(SignatureStatus.Broken, signature.Status);
    }

    [Fact]
    public void CambiareIlContenutoRompeLaFirma()
    {
        var parts = Parts.Sample();
        using var certificate = Certificate();

        var document = Signed(parts, certificate);

        parts.Replace("content.xml", "<office:document-content>manomesso</office:document-content>");

        Assert.Equal(SignatureStatus.Broken, Assert.Single(_builder.Verify(document, parts)).Status);
    }

    [Fact]
    public void AggiungereUnaParteRompeLaFirma()
    {
        var parts = Parts.Sample();
        using var certificate = Certificate();

        var document = Signed(parts, certificate);

        parts.Replace("Pictures/nuova.png", "finta");

        Assert.Equal(SignatureStatus.Broken, Assert.Single(_builder.Verify(document, parts)).Status);
    }

    [Fact]
    public void CambiareLaDataDichiarataRompeLaFirma()
    {
        var parts = Parts.Sample();
        using var certificate = Certificate();

        var document = Signed(parts, certificate);

        document.Descendants(XmlSignatureBuilder.Dc + "date").First().Value = "2000-01-01T00:00:00";

        Assert.Equal(SignatureStatus.Broken, Assert.Single(_builder.Verify(document, parts)).Status);
    }

    [Fact]
    public void UnCertificatoAutofirmatoNonEAttendibile()
    {
        var parts = Parts.Sample();
        using var certificate = Certificate();

        Assert.Equal(
            SignatureStatus.UntrustedCertificate,
            Assert.Single(_builder.Verify(Signed(parts, certificate), parts)).Status);
    }

    [Fact]
    public void SenzaFirmeNonSiVerificaNulla()
    {
        Assert.Empty(_builder.Verify(null, Parts.Sample()));
    }

    [Fact]
    public void DueFirmeConvivono()
    {
        var parts = Parts.Sample();
        using var first = Certificate("CN=Prima");
        using var second = Certificate("CN=Seconda");

        var document = new XElement(
            XmlSignatureBuilder.Document + "document-signatures",
            _builder.Sign(parts, first, DateTimeOffset.Now),
            _builder.Sign(parts, second, DateTimeOffset.Now));

        var signatures = _builder.Verify(document, parts);

        Assert.Equal(2, signatures.Count);
        Assert.Equal(["Prima", "Seconda"], signatures.Select(signature => signature.Signer.Name));
    }
}

public sealed class OdfSignatureServiceTests
{
    private readonly OdfSignatureService _service = new(new XmlSignatureBuilder());

    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);

        return new CertificateRequest("CN=Firmatario", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(365));
    }

    private static string Package(
        string extension = ".ods", string mimeType = "application/vnd.oasis.opendocument.spreadsheet")
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);

        using var archive = new ZipArchive(File.Create(path), ZipArchiveMode.Create);

        void Add(string name, string content, CompressionLevel level = CompressionLevel.Optimal)
        {
            using var stream = archive.CreateEntry(name, level).Open();
            stream.Write(Encoding.UTF8.GetBytes(content));
        }

        Add("mimetype", mimeType, CompressionLevel.NoCompression);
        Add("content.xml", "<office:document-content/>");
        Add("META-INF/manifest.xml", "<manifest/>");

        return path;
    }

    [Fact]
    public void UnPacchettoFirmatoRipresentaLaPropriaFirma()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();

            _service.Sign(path, certificate);

            var signature = Assert.Single(_service.Read(path));

            Assert.Equal("Firmatario", signature.Signer.Name);
            Assert.Contains("content.xml", signature.SignedParts);
            Assert.NotEqual(SignatureStatus.Broken, signature.Status);

            using var archive = System.IO.Compression.ZipFile.OpenRead(path);
            using var reader = new StreamReader(archive.GetEntry("META-INF/documentsignatures.xml")!.Open());
            Assert.DoesNotContain("\n  <", reader.ReadToEnd(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AncheUnaPresentazioneSiFirma()
    {
        var path = Package(".odp", "application/vnd.oasis.opendocument.presentation");

        try
        {
            using var certificate = Certificate();

            Assert.True(_service.CanSign(path));
            _service.Sign(path, certificate);

            var signature = Assert.Single(_service.Read(path));

            Assert.Equal("Firmatario", signature.Signer.Name);
            Assert.Contains("content.xml", signature.SignedParts);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ModificareIlPacchettoDopoLaFirmaLaRompe()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();
            _service.Sign(path, certificate);

            using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                archive.GetEntry("content.xml")!.Delete();

                using var stream = archive.CreateEntry("content.xml").Open();
                stream.Write(Encoding.UTF8.GetBytes("<office:document-content>altro</office:document-content>"));
            }

            Assert.Equal(SignatureStatus.Broken, Assert.Single(_service.Read(path)).Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LaFirmaSiPuoTogliere()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();
            _service.Sign(path, certificate);

            Assert.True(_service.Remove(path, certificate.Thumbprint));
            Assert.Empty(_service.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IlMimetypeRestaLaPrimaVoceNonCompressa()
    {
        var path = Package();

        try
        {
            using var certificate = Certificate();
            _service.Sign(path, certificate);

            using var archive = ZipFile.OpenRead(path);

            Assert.Equal("mimetype", archive.Entries[0].FullName);
            Assert.Equal(archive.Entries[0].Length, archive.Entries[0].CompressedLength);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnPacchettoSenzaFirmeNonNeDichiara()
    {
        var path = Package();

        try
        {
            Assert.Empty(_service.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public sealed class MacroSignerTests
{
    private readonly MacroSigner _signer = new();

    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);

        return new CertificateRequest("CN=Autore", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(30));
    }

    private static (string, string, string)[] Macros() =>
    [
        ("Saluta", "JavaScript", "foglio.Write('A1', 'ciao')"),
        ("Conta", "JavaScript", "foglio.Write('A2', 42)"),
    ];

    [Fact]
    public void LImprontaNonDipendeDallOrdine()
    {
        var first = _signer.Fingerprint(Macros());
        var second = _signer.Fingerprint([Macros()[1], Macros()[0]]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void LImprontaNonDipendeDallOrdineNeancheConNomiRipetuti()
    {
        (string, string, string) javascript = ("Avvio", "JavaScript", "a()");
        (string, string, string) python = ("Avvio", "Python", "b()");

        Assert.Equal(_signer.Fingerprint([javascript, python]), _signer.Fingerprint([python, javascript]));
    }

    [Fact]
    public void UnaMacroFirmataSiVerifica()
    {
        using var certificate = Certificate();

        var signature = _signer.Sign(Macros(), certificate);

        Assert.Equal("Autore", signature.Signer.Name);
        Assert.NotEqual(SignatureStatus.Broken, _signer.Verify(Macros(), signature));
    }

    [Fact]
    public void CambiareUnaMacroRompeLaFirma()
    {
        using var certificate = Certificate();

        var signature = _signer.Sign(Macros(), certificate);

        (string, string, string)[] changed = [("Saluta", "JavaScript", "foglio.Write('A1', 'altro')"), Macros()[1]];

        Assert.Equal(SignatureStatus.Broken, _signer.Verify(changed, signature));
    }

    [Fact]
    public void CambiareIlLinguaggioRompeLaFirma()
    {
        using var certificate = Certificate();

        var signature = _signer.Sign(Macros(), certificate);

        (string, string, string)[] changed = [("Saluta", "Python", "foglio.Write('A1', 'ciao')"), Macros()[1]];

        Assert.Equal(SignatureStatus.Broken, _signer.Verify(changed, signature));
    }

    [Fact]
    public void AggiungereUnaMacroRompeLaFirma()
    {
        using var certificate = Certificate();

        var signature = _signer.Sign(Macros(), certificate);

        Assert.Equal(
            SignatureStatus.Broken,
            _signer.Verify([.. Macros(), ("Nuova", "JavaScript", "x")], signature));
    }

    [Fact]
    public void SenzaFirmaLoStatoENoto()
    {
        Assert.Equal(SignatureStatus.Unknown, _signer.Verify(Macros(), null));
    }
}

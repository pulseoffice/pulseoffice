using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace PulseOffice.Security.Tests;

public sealed class CertificateFileTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulseoffice-p12-").FullName;

    [Fact]
    public void Un_certificato_in_un_file_PKCS12_si_carica_con_la_chiave_e_firma_un_ODT()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Ada Lovelace Prova", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var created = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(30));
        var file = Path.Combine(_folder, "prova.p12");
        File.WriteAllBytes(file, created.Export(X509ContentType.Pkcs12, "prova123"));

        using var certificate = new CertificateSource().FromFile(file, "prova123");

        Assert.NotNull(certificate);
        Assert.True(certificate.HasPrivateKey);
        Assert.Null(new CertificateSource().FromFile(file, "sbagliata"));

        var document = Path.Combine(_folder, "firmato.odt");
        using (var zip = System.IO.Compression.ZipFile.Open(document, System.IO.Compression.ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("mimetype", System.IO.Compression.CompressionLevel.NoCompression).Open())) writer.Write("application/vnd.oasis.opendocument.text");
            using (var writer = new StreamWriter(zip.CreateEntry("content.xml").Open())) writer.Write("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.3\"/>");
            using (var writer = new StreamWriter(zip.CreateEntry("META-INF/manifest.xml").Open()))
                writer.Write("<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\"><manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\"/><manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/></manifest:manifest>");
        }

        var service = new OdfSignatureService(new XmlSignatureBuilder());
        service.Sign(document, certificate);

        Assert.Equal(SignatureStatus.UntrustedCertificate, Assert.Single(service.Read(document)).Status);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}

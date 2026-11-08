using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PulseOffice.Security;
using Xunit;

namespace PulseOffice.Security.Tests;

public sealed class Round24CryptoTests
{
    private static X509Certificate2 Certificate(string name)
    {
        using var key = RSA.Create(2048);
        return new CertificateRequest(name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(365));
    }

    [Fact]
    public void Changing_the_content_type_of_a_signed_ooxml_part_breaks_the_signature()
    {
        var algorithms = new XmlDsigAlgorithms();
        var service = new OoxmlSignatureService(
            new OoxmlSignatureBuilder(algorithms, new RelationshipTransform(algorithms), new OoxmlSignatureScope(), new CertificateJudge()),
            new OpcPackageStore());
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".docx");

        try
        {
            using (var archive = new ZipArchive(File.Create(path), ZipArchiveMode.Create))
            {
                Add(archive, "[Content_Types].xml",
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                    + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                    + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                    + "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>"
                    + "</Types>");
                Add(archive, "_rels/.rels",
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                    + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>"
                    + "</Relationships>");
                Add(archive, "word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"/>");
            }

            using var certificate = Certificate("CN=Firmatario");
            service.Sign(path, certificate);
            Assert.NotEqual(SignatureStatus.Broken, Assert.Single(service.Read(path)).Status);

            using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                var entry = archive.GetEntry("[Content_Types].xml")!;
                string types;
                using (var reader = new StreamReader(entry.Open())) types = reader.ReadToEnd();
                entry.Delete();
                Add(archive, "[Content_Types].xml", types.Replace(
                    "wordprocessingml.document.main+xml", "wordprocessingml.template.main+xml", StringComparison.Ordinal));
            }

            Assert.Equal(SignatureStatus.Broken, Assert.Single(service.Read(path)).Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void An_odf_signature_that_does_not_cover_the_manifest_is_broken()
    {
        var builder = new XmlSignatureBuilder();
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["mimetype"] = Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.text"),
            ["content.xml"] = Encoding.UTF8.GetBytes("<office:document-content/>"),
        };
        using var certificate = Certificate("CN=Firmatario");
        var signatures = new System.Xml.Linq.XElement(
            XmlSignatureBuilder.Document + "document-signatures", builder.Sign(new Parts(entries), certificate, DateTimeOffset.Now));

        entries["META-INF/manifest.xml"] = Encoding.UTF8.GetBytes("<manifest/>");

        Assert.Equal(SignatureStatus.Broken, Assert.Single(builder.Verify(signatures, new Parts(entries))).Status);
    }

    [Fact]
    public void A_later_untrusted_pdf_signature_does_not_vouch_for_changes_after_a_trusted_one()
    {
        using var trusted = Certificate("CN=Banca");
        using var stranger = Certificate("CN=Estraneo");
        var judge = new CertificateJudge(
            new CustomRootCertificateTrust([trusted]), new TrustedTimestampVerifier(), TimeProvider.System);
        var signer = new PdfSigner(judge);
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");

        try
        {
            var text = "%PDF-1.7\n";
            var first = text.Length;
            text += "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n";
            var second = text.Length;
            text += "2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\n";
            var xref = text.Length;
            text += $"xref\n0 3\n0000000000 65535 f \n{first:D10} 00000 n \n{second:D10} 00000 n \ntrailer\n<< /Size 3 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n";
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(text));

            signer.Sign(path, trusted);
            Assert.Equal(SignatureStatus.Valid, Assert.Single(signer.Read(path)).Status);

            signer.Sign(path, stranger);

            Assert.DoesNotContain(signer.Read(path), signature => signature.Status == SignatureStatus.Valid);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void Add(ZipArchive archive, string name, string content)
    {
        using var stream = archive.CreateEntry(name).Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
    }
}

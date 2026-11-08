using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using PulseOffice.Document.Fonts;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.Packaging.Encryption;
using PulseOffice.Security;
using PulseOffice.Typography.Tests;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtPackageContourTests
{
    private readonly TextDocumentDescriber _describer = new();

    [Fact]
    public void ICaratteriIncorporatiELeVersioniSopravvivono()
    {
        var document = OdtRoundTripTests.Sample();
        document.Fonts = new DocumentFonts(true,
        [
            new EmbeddedFontFile("Prova Sans", 400, false, "a.ttf", new SyntheticFont().Build()),
        ]);
        document.Versions.Add([1, 2, 3], "Ada", "prima");

        var read = OdtRoundTripTests.RoundTrip(document);

        Assert.Equal(["Prova Sans"], read.Fonts.Families);
        Assert.True(read.Fonts.EmbedSystemFonts);
        Assert.Equal(1, read.Versions.Count);
        Assert.Equal(_describer.Describe(document), _describer.Describe(read));
    }

    [Fact]
    public void OgniVoceDelPacchettoEDichiarataNelManifest()
    {
        var document = OdtRoundTripTests.Sample();
        document.Versions.Add([1, 2, 3], "Ada", "prima");
        using var stream = new MemoryStream();
        new OdtWriter().Write(document, stream);
        stream.Position = 0;

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Equal("mimetype", archive.Entries[0].FullName);
        var manifest = XDocument.Load(archive.GetEntry("META-INF/manifest.xml")!.Open());
        XNamespace m = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
        var declared = manifest.Descendants(m + "file-entry").Select(entry => (string)entry.Attribute(m + "full-path")!).ToHashSet();
        foreach (var entry in archive.Entries.Where(entry => entry.FullName != "mimetype" && !entry.FullName.StartsWith("META-INF/", StringComparison.Ordinal)))
            Assert.Contains(entry.FullName, declared);
    }

    [Fact]
    public void UnOdtCifratoSiRiapreConLaPassword()
    {
        using var plain = new MemoryStream();
        new OdtWriter().Write(OdtRoundTripTests.Sample(), plain);
        plain.Position = 0;

        var encryption = new OdfPackageEncryption();
        using var encrypted = encryption.Encrypt(plain, new PasswordProtection("segreta"));
        encrypted.Position = 0;
        Assert.Equal("ODF004", new OdtReader().Read(encrypted).Diagnostics[0].Code);

        encrypted.Position = 0;
        var decrypted = encryption.Decrypt(encrypted, new OdfPassword("segreta"));
        Assert.Equal(OdfDecryptionStatus.Decrypted, decrypted.Status);
        var read = new OdtReader().Read(decrypted.Package!);
        Assert.True(read.Succeeded);
        Assert.Equal(_describer.Describe(OdtRoundTripTests.Sample()), _describer.Describe(read.Value!));
    }

    [Fact]
    public void UnOdtSiFirmaESiVerifica()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".odt");
        try
        {
            using (var file = File.Create(path)) new OdtWriter().Write(OdtRoundTripTests.Sample(), file);
            using var key = RSA.Create(2048);
            using var certificate = new CertificateRequest("CN=Firmatario", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(365));
            var service = new OdfSignatureService(new XmlSignatureBuilder());

            service.Sign(path, certificate);

            var signature = Assert.Single(service.Read(path));
            Assert.NotEqual(SignatureStatus.Broken, signature.Status);
            using var stream = File.OpenRead(path);
            Assert.True(new OdtReader().Read(stream).Succeeded);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("unoapi/testdocuments/XDocumentInsertable.sxw")]
    [InlineData("unoapi/testdocuments/MailMerge.sxw")]
    [InlineData("extras/ooxmlexport/data/ooo30436-1-minimized.sxw")]
    [InlineData("core/data/xml/pass/ooo32780-1.sxw")]
    public void UnFileOpenOfficeSxwSiApre(string relative)
    {
        var path = Path.Combine("/Volumes/Dati/develop/libreoffice-ref/sw/qa", relative);
        if (!File.Exists(path)) Assert.Skip("Reference corpus not available.");
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        Assert.True(new OdtReader().CanRead(stream));
        var read = new OdtReader().Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        Assert.NotEmpty(read.Value!.Body.Blocks);
    }
}

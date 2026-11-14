using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Platform;
using PulseOffice.Printing;
using PulseOffice.Security;
using PulseOffice.Typography.OpenType;
using PulseOffice.Typography.OpenType.Metrics;
using PulseOffice.Typography.Subsetting;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class PdfVisibleSignatureFontTests
{
    private static readonly Lazy<IndexedFontLocator> Installed = new(() =>
        new IndexedFontLocator(new SystemFontDirectories(new PulseOffice.Platform.SystemEnvironmentVariables(), ((PulseOffice.Platform.IHostSystem)new PulseOffice.Platform.RuntimeHostSystem()).Family), new OpenTypeReader(), new FontMetricsReader()));

    private sealed class OneLine : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => 1;

        public void PaintPage(int index, IPageCanvas canvas) =>
            canvas.DrawText("Contratto", new DocumentRect(Twip.FromPoints(50), Twip.FromPoints(60), Twip.FromPoints(500), Twip.FromPoints(24)), new PrintFont("Arial", 12), PrintColor.Black);
    }

    private static PdfFontSetFactory Fonts() => new(
        new PdfFaceLocator(Installed.Value, Installed.Value), new PdfFallbackFonts(), new TrueTypeSubsetter(), new ToUnicodeWriter(), new PdfFontMetrics(),
        new PulseOffice.Typography.Shaping.HarfBuzzTextShaper(), new PulseOffice.Typography.Shaping.ComplexScriptSegmenter());

    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);
        return new CertificateRequest("CN=Firmatario Àccentato", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(365));
    }

    [Fact]
    public void AVisiblySignedArchivePdfEmbedsTheAppearanceFont()
    {
        if (!PdfFallbackFontsInstalled()) return;
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");
        try
        {
            using (var stream = File.Create(path))
                Assert.True(new PdfExporter(Fonts()).Write(new OneLine(), stream, new PdfExportOptions("Prova", Compress: false) { Archive = true }).Succeeded);
            var exported = new FileInfo(path).Length;
            var signer = new PdfSigner(new CertificateJudge(), new EmbeddedPdfSignatureFont(Fonts()));
            using var certificate = Certificate();

            signer.Sign(path, certificate, "Approvazione ΑΒΓ", "Torino", new PdfSignatureAppearance("Firmato digitalmente da", "Data", "Motivo", "Luogo"));

            var appended = Encoding.Latin1.GetString(File.ReadAllBytes(path)[(int)exported..]);
            Assert.DoesNotContain("/BaseFont /Helvetica", appended, StringComparison.Ordinal);
            Assert.Contains("/FontFile2", appended, StringComparison.Ordinal);
            Assert.Matches(@"/BaseFont /[A-Z]{6}\+", appended);
            Assert.Contains("/ToUnicode", appended, StringComparison.Ordinal);
            Assert.Contains("] TJ T*", appended, StringComparison.Ordinal);
            var signature = Assert.Single(signer.Read(path));
            Assert.NotEqual(SignatureStatus.Broken, signature.Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool PdfFallbackFontsInstalled() =>
        new PdfFallbackFonts().Families.Any(family => Installed.Value.Find(family, false, false) is { HasTrueTypeOutlines: true });
}

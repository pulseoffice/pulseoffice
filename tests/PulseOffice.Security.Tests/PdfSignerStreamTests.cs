using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using PulseOffice.Security;
using Xunit;

namespace PulseOffice.Security.Tests;

public sealed class PdfSignerStreamTests
{
    private readonly PdfSigner _signer = new();

    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);
        return new CertificateRequest("CN=Firmatario Terzo", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(365));
    }

    private static string CompressedPdf(string pageExtra = "")
    {
        var file = new MemoryStream();
        void Write(string text) => file.Write(Encoding.Latin1.GetBytes(text));

        Write("%PDF-1.7\n%âãÏÓ\n");
        var offsets = new Dictionary<int, long>();

        offsets[3] = file.Length;
        Write($"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842]{pageExtra} >>\nendobj\n");

        var packed = "<< /Type /Catalog /Pages 2 0 R >>\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\n";
        var second = packed.IndexOf('\n') + 1;
        var header = $"1 0 2 {second} ";
        var plain = Encoding.Latin1.GetBytes(header + packed);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(plain);
        offsets[4] = file.Length;
        Write($"4 0 obj\n<< /Type /ObjStm /N 2 /First {header.Length} /Filter /FlateDecode /Length {compressed.Length} >>\nstream\n");
        file.Write(compressed.ToArray());
        Write("\nendstream\nendobj\n");

        var xref = file.Length;
        var entries = new MemoryStream();
        void Entry(byte type, long field, int second16)
        {
            entries.WriteByte(type);
            entries.Write([(byte)(field >> 24), (byte)(field >> 16), (byte)(field >> 8), (byte)field]);
            entries.Write([(byte)(second16 >> 8), (byte)second16]);
        }
        Entry(0, 0, 65535);
        Entry(2, 4, 0);
        Entry(2, 4, 1);
        Entry(1, offsets[3], 0);
        Entry(1, offsets[4], 0);
        Entry(1, xref, 0);
        Write($"5 0 obj\n<< /Type /XRef /Size 6 /W [1 4 2] /Root 1 0 R /ID [<0A0B> <0A0B>] /Length {entries.Length} >>\nstream\n");
        file.Write(entries.ToArray());
        Write($"\nendstream\nendobj\nstartxref\n{xref}\n%%EOF\n");

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");
        File.WriteAllBytes(path, file.ToArray());
        return path;
    }

    private static Dictionary<int, long> LastCrossReferenceStream(byte[] bytes)
    {
        var text = Encoding.Latin1.GetString(bytes);
        var start = long.Parse(Regex.Match(text, @"startxref\s+(\d+)\s*%%EOF\s*$", RegexOptions.RightToLeft).Groups[1].Value, CultureInfo.InvariantCulture);
        var dictionaryEnd = text.IndexOf("stream\n", (int)start, StringComparison.Ordinal);
        var dictionary = text[(int)start..dictionaryEnd];
        Assert.Contains("/Type /XRef", dictionary, StringComparison.Ordinal);
        Assert.Contains("/W [1 4 2]", dictionary, StringComparison.Ordinal);
        Assert.Contains("/Prev ", dictionary, StringComparison.Ordinal);
        Assert.Contains("/ID [<0A0B> <0A0B>]", dictionary, StringComparison.Ordinal);
        var index = Regex.Match(dictionary, @"/Index \[([^\]]*)\]").Groups[1].Value.Split(' ').Select(int.Parse).ToList();
        var data = dictionaryEnd + 7;
        var offsets = new Dictionary<int, long>();
        for (var pair = 0; pair < index.Count; pair += 2)
        {
            for (var i = 0; i < index[pair + 1]; i++)
            {
                Assert.Equal(1, bytes[data]);
                offsets[index[pair] + i] = (bytes[data + 1] << 24) | (bytes[data + 2] << 16) | (bytes[data + 3] << 8) | bytes[data + 4];
                data += 7;
            }
        }
        return offsets;
    }

    [Fact]
    public void AFileEndingWithACrossReferenceStreamIsUpdatedWithAStream()
    {
        var path = CompressedPdf();
        try
        {
            using var certificate = Certificate();
            _signer.Sign(path, certificate);

            var bytes = File.ReadAllBytes(path);
            var text = Encoding.Latin1.GetString(bytes);
            var offsets = LastCrossReferenceStream(bytes);

            Assert.DoesNotContain("trailer", text, StringComparison.Ordinal);
            foreach (var (id, offset) in offsets)
                Assert.StartsWith($"{id} 0 obj", text[(int)offset..], StringComparison.Ordinal);
            Assert.Contains(1, offsets.Keys);
            Assert.Equal(SignatureStatus.UntrustedCertificate, Assert.Single(_signer.Read(path)).Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheCatalogueInsideAnObjectStreamIsFound()
    {
        var path = CompressedPdf();
        try
        {
            using var certificate = Certificate();
            _signer.Sign(path, certificate);

            var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
            var catalogue = Regex.Match(text, @"\n1 0 obj\n(.*?)\nendobj", RegexOptions.Singleline).Groups[1].Value;
            Assert.Contains("/Pages 2 0 R", catalogue, StringComparison.Ordinal);
            Assert.Contains("/AcroForm", catalogue, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AVisibleSignatureIsAnnotationOfTheFirstPageWithAnAppearance()
    {
        var path = CompressedPdf();
        try
        {
            using var certificate = Certificate();
            _signer.Sign(path, certificate, "Approvazione", "Torino", new PdfSignatureAppearance("Firmato digitalmente da", "Data", "Motivo", "Luogo"));

            var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
            var page = Regex.Matches(text, @"\n3 0 obj\n(.*?)\nendobj", RegexOptions.Singleline)[^1].Groups[1].Value;
            var widgetId = int.Parse(Regex.Match(page, @"/Annots \[(\d+) 0 R\]").Groups[1].Value, CultureInfo.InvariantCulture);
            var widget = Regex.Match(text, $@"\n{widgetId} 0 obj\n(.*?)\nendobj", RegexOptions.Singleline).Groups[1].Value;

            Assert.Contains("/FT /Sig", widget, StringComparison.Ordinal);
            Assert.Contains("/P 3 0 R", widget, StringComparison.Ordinal);
            Assert.Contains("/F 4", widget, StringComparison.Ordinal);
            Assert.DoesNotContain("/Rect [0 0 0 0]", widget, StringComparison.Ordinal);
            var appearance = int.Parse(Regex.Match(widget, @"/AP << /N (\d+) 0 R >>").Groups[1].Value, CultureInfo.InvariantCulture);
            var form = Regex.Match(text, $@"\n{appearance} 0 obj\n(.*?)\nendobj", RegexOptions.Singleline).Groups[1].Value;
            Assert.Contains("/Subtype /Form", form, StringComparison.Ordinal);
            Assert.Contains("(Firmato digitalmente da Firmatario Terzo) Tj", form, StringComparison.Ordinal);
            Assert.Contains("(Luogo: Torino) Tj", form, StringComparison.Ordinal);
            Assert.Single(_signer.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AVisibleSignatureAppendsToExistingAnnotations()
    {
        var path = CompressedPdf(" /Annots [9 0 R]");
        try
        {
            using var certificate = Certificate();
            _signer.Sign(path, certificate, appearance: new PdfSignatureAppearance());

            var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
            var page = Regex.Matches(text, @"\n3 0 obj\n(.*?)\nendobj", RegexOptions.Singleline)[^1].Groups[1].Value;
            Assert.Matches(@"/Annots \[9 0 R \d+ 0 R\]", page);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnInvisibleSignatureLeavesThePageAlone()
    {
        var path = CompressedPdf();
        try
        {
            using var certificate = Certificate();
            _signer.Sign(path, certificate);

            var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
            Assert.Single(Regex.Matches(text, @"\n3 0 obj\n"));
            Assert.Contains("/Rect [0 0 0 0]", text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

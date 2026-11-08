using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PulseOffice.Security;
using Xunit;

namespace PulseOffice.Security.Tests;

public sealed class PdfSignerTests
{
    private readonly PdfSigner _signer = new();

    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);

        return new CertificateRequest("CN=Firmatario", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(365));
    }

    private static string Pdf(string trailer = "")
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");

        var builder = new StringBuilder();
        var offsets = new List<int>();

        void Object(string body)
        {
            offsets.Add(builder.Length);
            builder.Append(body);
        }

        builder.Append("%PDF-1.7\n");

        Object("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Object("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        Object("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>\nendobj\n");

        var startXref = builder.Length;

        builder.Append("xref\n0 4\n0000000000 65535 f \n");

        foreach (var offset in offsets) builder.Append(offset.ToString("D10") + " 00000 n \n");

        builder.Append($"trailer\n<< /Size 4 /Root 1 0 R{trailer} >>\n");
        builder.Append($"startxref\n{startXref}\n%%EOF\n");

        File.WriteAllBytes(path, Encoding.Latin1.GetBytes(builder.ToString()));

        return path;
    }

    [Fact]
    public void UnFileQualunqueNonEUnPdf()
    {
        var path = Path.GetTempFileName();

        try
        {
            File.WriteAllText(path, "non sono un pdf");

            Assert.False(_signer.IsPdf(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnPdfSiRiconosce()
    {
        var path = Pdf();

        try
        {
            Assert.True(_signer.IsPdf(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnPdfFirmatoRipresentaLaPropriaFirma()
    {
        var path = Pdf();

        try
        {
            using var certificate = Certificate();

            _signer.Sign(path, certificate, "Approvazione", "Torino");

            var signature = Assert.Single(_signer.Read(path));

            Assert.Equal("Firmatario", signature.Signer.Name);
            Assert.NotEqual(SignatureStatus.Broken, signature.Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IlFileFirmatoRestaUnPdfValido()
    {
        var path = Pdf();

        try
        {
            using var certificate = Certificate();

            var before = new FileInfo(path).Length;

            _signer.Sign(path, certificate);

            var bytes = File.ReadAllBytes(path);
            var text = Encoding.Latin1.GetString(bytes);

            Assert.True(bytes.Length > before);
            Assert.StartsWith("%PDF-", text, StringComparison.Ordinal);
            Assert.EndsWith("%%EOF\n", text, StringComparison.Ordinal);
            Assert.Contains("/SubFilter /adbe.pkcs7.detached", text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ManometterePagineDopoLaFirmaLaRompe()
    {
        var path = Pdf();

        try
        {
            using var certificate = Certificate();
            _signer.Sign(path, certificate);

            var bytes = File.ReadAllBytes(path);
            var marker = Encoding.Latin1.GetString(bytes).IndexOf("/MediaBox", StringComparison.Ordinal);

            Assert.True(marker > 0);

            bytes[marker + 2] = (byte)'X';
            File.WriteAllBytes(path, bytes);

            Assert.Equal(SignatureStatus.Broken, Assert.Single(_signer.Read(path)).Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnAggiornamentoAggiuntoDopoLaFirmaLaRompe()
    {
        var path = Pdf();

        try
        {
            using var certificate = Certificate();
            _signer.Sign(path, certificate);

            File.AppendAllText(path, "\n9 0 obj\n<< /Type /Page >>\nendobj\n", Encoding.Latin1);

            Assert.Equal(SignatureStatus.Broken, Assert.Single(_signer.Read(path)).Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnAggiornamentoDopoDueFirmeLeRompeEntrambe()
    {
        var path = Pdf();

        try
        {
            using var certificate = Certificate();
            _signer.Sign(path, certificate);
            _signer.Sign(path, certificate);

            File.AppendAllText(path, "\n9 0 obj\n<< /Type /Page >>\nendobj\n", Encoding.Latin1);

            Assert.All(_signer.Read(path), signature => Assert.Equal(SignatureStatus.Broken, signature.Status));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FirmareDueVolteNonRompeIlFile()
    {
        var path = Pdf();

        try
        {
            using var certificate = Certificate();
            _signer.Sign(path, certificate);
            _signer.Sign(path, certificate);

            var signatures = _signer.Read(path);
            Assert.Equal(2, signatures.Count);
            Assert.All(signatures, signature => Assert.NotEqual(SignatureStatus.Broken, signature.Status));

            var text = File.ReadAllText(path, Encoding.Latin1);
            Assert.Matches(@"/Fields \[\d+ 0 R \d+ 0 R\]", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IlCatalogoOriginaleNonVieneInventato()
    {
        var path = Pdf();

        try
        {
            using var certificate = Certificate();

            _signer.Sign(path, certificate);

            var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
            var last = text.LastIndexOf("/Type /Catalog", StringComparison.Ordinal);

            Assert.True(last > 0);

            var tail = text[last..];
            var close = tail.IndexOf("endobj", StringComparison.Ordinal);

            Assert.Contains("/Pages 2 0 R", tail[..close], StringComparison.Ordinal);
            Assert.Contains("/AcroForm", tail[..close], StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnPdfNonFirmatoNonDichiaraFirme()
    {
        var path = Pdf();

        try
        {
            Assert.Empty(_signer.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LAggiornamentoRiportaInformazioniEIdentificativo()
    {
        var path = Pdf(" /Info 3 0 R /ID [<00AA> <00AA>]");

        try
        {
            using var certificate = Certificate();
            _signer.Sign(path, certificate);

            var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
            var last = text[text.LastIndexOf("trailer", StringComparison.Ordinal)..];
            Assert.Contains("/Info 3 0 R", last, StringComparison.Ordinal);
            Assert.Contains("/ID [<00AA> <00AA>]", last, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnPdfCifratoNonSiFirmaENonSiRovina()
    {
        var path = Pdf(" /Encrypt 3 0 R");

        try
        {
            var before = File.ReadAllBytes(path);
            using var certificate = Certificate();

            Assert.Throws<InvalidOperationException>(() => _signer.Sign(path, certificate));
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnaFirmaScrittaSenzaSpaziSiRiconosceConLaSuaData()
    {
        var path = Pdf();

        try
        {
            using var certificate = Certificate();
            _signer.Sign(path, certificate);
            var bytes = File.ReadAllBytes(path);
            var text = Encoding.Latin1.GetString(bytes);
            var date = text.IndexOf("/M (D:", StringComparison.Ordinal) + 6;
            var compact = text[..date] + "20261106100000+05'30'" + text[(date + 21)..];
            compact = compact.Replace("/ByteRange [", "/ByteRange[ ", StringComparison.Ordinal).Replace("/SubFilter /adbe.pkcs7.detached", "/SubFilter/adbe.pkcs7.detached ", StringComparison.Ordinal);
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(compact));

            var signature = Assert.Single(_signer.Read(path));
            Assert.Equal(new DateTimeOffset(2026, 11, 6, 10, 0, 0, new TimeSpan(5, 30, 0)), signature.SignedOn);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

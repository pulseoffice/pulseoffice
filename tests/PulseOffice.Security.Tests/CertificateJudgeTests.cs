using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PulseOffice.Security;
using Xunit;

namespace PulseOffice.Security.Tests;

public class CertificateJudgeTests
{
    private static X509Certificate2 Certificate(DateTimeOffset from, DateTimeOffset to, X509KeyUsageFlags? usage = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Prova", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (usage is { } flags) request.CertificateExtensions.Add(new X509KeyUsageExtension(flags, critical: true));
        return request.CreateSelfSigned(from, to);
    }

    [Fact]
    public void UnCertificatoScadutoOggiSenzaMarcaTemporaleNonEValido()
    {
        using var certificate = Certificate(DateTimeOffset.Now.AddYears(-2), DateTimeOffset.Now.AddDays(-1));

        Assert.NotEqual(SignatureStatus.Valid, new CertificateJudge().Judge(certificate));
    }

    [Fact]
    public void UnCertificatoSenzaUsoDiFirmaNonEAttendibile()
    {
        using var certificate = Certificate(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1), X509KeyUsageFlags.KeyEncipherment);

        Assert.Equal(SignatureStatus.UntrustedCertificate, new CertificateJudge().Judge(certificate));
    }
}

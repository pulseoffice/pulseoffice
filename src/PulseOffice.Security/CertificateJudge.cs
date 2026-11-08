using System.Security.Cryptography.X509Certificates;

namespace PulseOffice.Security;

public sealed class CertificateJudge(ICertificateTrust trust, ITrustedTimestampVerifier timestamps, TimeProvider clock)
{
    public CertificateJudge() : this(new SystemCertificateTrust(), new TrustedTimestampVerifier(), TimeProvider.System)
    {
    }

    public SignatureStatus Judge(X509Certificate2 certificate, TimestampEvidence? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        if (timestamp is not null && timestamps.Verify(timestamp) is { } stamped)
            return JudgeAt(certificate, stamped.UtcDateTime);

        var today = clock.GetUtcNow().UtcDateTime;
        var notBefore = certificate.NotBefore.ToUniversalTime();
        var notAfter = certificate.NotAfter.ToUniversalTime();

        if (today < notBefore) return SignatureStatus.ExpiredCertificate;
        if (today <= notAfter) return JudgeAt(certificate, today);

        if (!MaySign(certificate)) return SignatureStatus.UntrustedCertificate;
        return trust.Trusted(certificate, notAfter.AddSeconds(-1))
            ? SignatureStatus.UnverifiableInTime
            : SignatureStatus.UntrustedCertificate;
    }

    private SignatureStatus JudgeAt(X509Certificate2 certificate, DateTime moment)
    {
        if (moment > certificate.NotAfter.ToUniversalTime() || moment < certificate.NotBefore.ToUniversalTime())
            return SignatureStatus.ExpiredCertificate;
        if (!MaySign(certificate)) return SignatureStatus.UntrustedCertificate;

        return trust.Trusted(certificate, moment) ? SignatureStatus.Valid : SignatureStatus.UntrustedCertificate;
    }

    private bool MaySign(X509Certificate2 certificate) =>
        certificate.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault() is not { } usage
        || (usage.KeyUsages & (X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation)) != 0;
}

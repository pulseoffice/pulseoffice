using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace PulseOffice.Security;

public sealed record TimestampEvidence(byte[] Token, byte[] StampedData);

public interface ICertificateTrust
{
    bool Trusted(X509Certificate2 certificate, DateTime moment, X509Certificate2Collection? extra = null);
}

public abstract class ChainCertificateTrust : ICertificateTrust
{
    public bool Trusted(X509Certificate2 certificate, DateTime moment, X509Certificate2Collection? extra = null)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        using var chain = new X509Chain();
        var policy = chain.ChainPolicy;
        policy.RevocationMode = X509RevocationMode.Offline;
        policy.RevocationFlag = X509RevocationFlag.EntireChain;
        policy.VerificationFlags = X509VerificationFlags.IgnoreEndRevocationUnknown
            | X509VerificationFlags.IgnoreCertificateAuthorityRevocationUnknown
            | X509VerificationFlags.IgnoreCtlSignerRevocationUnknown
            | X509VerificationFlags.IgnoreRootRevocationUnknown;
        policy.VerificationTime = moment;
        if (extra is not null) policy.ExtraStore.AddRange(extra);
        Adjust(policy);

        try
        {
            return chain.Build(certificate);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    protected abstract void Adjust(X509ChainPolicy policy);
}

public sealed class SystemCertificateTrust : ChainCertificateTrust
{
    protected override void Adjust(X509ChainPolicy policy)
    {
    }
}

public sealed class CustomRootCertificateTrust(X509Certificate2Collection roots) : ChainCertificateTrust
{
    protected override void Adjust(X509ChainPolicy policy)
    {
        policy.RevocationMode = X509RevocationMode.NoCheck;
        policy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        policy.CustomTrustStore.AddRange(roots);
    }
}

public interface ITrustedTimestampVerifier
{
    DateTimeOffset? Verify(TimestampEvidence evidence);
}

public sealed class TrustedTimestampVerifier(ICertificateTrust trust, TimeProvider clock) : ITrustedTimestampVerifier
{
    public TrustedTimestampVerifier() : this(new SystemCertificateTrust())
    {
    }

    public TrustedTimestampVerifier(ICertificateTrust trust) : this(trust, TimeProvider.System)
    {
    }

    public DateTimeOffset? Verify(TimestampEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        try
        {
            if (!Rfc3161TimestampToken.TryDecode(evidence.Token, out var token, out _)) return null;
            if (!token.VerifySignatureForData(evidence.StampedData, out var authority)) return null;
            if (authority is null) return null;

            var moment = token.TokenInfo.Timestamp;
            if (moment > clock.GetUtcNow().AddMinutes(5)) return null;

            var extra = new X509Certificate2Collection();
            extra.AddRange(token.AsSignedCms().Certificates);

            return trust.Trusted(authority, moment.UtcDateTime, extra) ? moment : null;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException or FormatException)
        {
            return null;
        }
    }
}

public sealed class XadesTimestampLocator(XmlDsigAlgorithms algorithms)
{
    public TimestampEvidence? Find(System.Xml.Linq.XElement signature)
    {
        ArgumentNullException.ThrowIfNull(signature);

        var value = signature.Element(XmlSignatureBuilder.Signature + "SignatureValue");
        var encapsulated = signature.Descendants()
            .Where(node => node.Name.LocalName == "SignatureTimeStamp")
            .SelectMany(node => node.Elements().Where(child => child.Name.LocalName == "EncapsulatedTimeStamp"))
            .FirstOrDefault();

        if (value is null || encapsulated is null) return null;

        try
        {
            return new TimestampEvidence(Convert.FromBase64String(encapsulated.Value.Trim()), algorithms.Canonical(value));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

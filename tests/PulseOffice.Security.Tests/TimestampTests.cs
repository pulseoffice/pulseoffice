using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Xunit;

namespace PulseOffice.Security.Tests;

public sealed class TimestampTests : IDisposable
{
    private readonly X509Certificate2 _authority = Authority();

    public void Dispose() => _authority.Dispose();

    private static X509Certificate2 Signer(DateTimeOffset from, DateTimeOffset to)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Firmatario", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        return request.CreateSelfSigned(from, to);
    }

    private static X509Certificate2 Authority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Marca temporale di prova", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.8")], critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        return request.CreateSelfSigned(DateTimeOffset.Now.AddYears(-5), DateTimeOffset.Now.AddYears(5));
    }

    private CertificateJudge Judge(params X509Certificate2[] trusted)
    {
        var trust = new CustomRootCertificateTrust([.. trusted, _authority]);
        return new CertificateJudge(trust, new TrustedTimestampVerifier(trust), TimeProvider.System);
    }

    private byte[] Token(byte[] stamped, DateTimeOffset moment) => Token(stamped, moment, _authority);

    private byte[] Token(byte[] stamped, DateTimeOffset moment, X509Certificate2 authority)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(1);
            writer.WriteObjectIdentifier("1.2.3.4.1");
            using (writer.PushSequence())
            {
                using (writer.PushSequence()) writer.WriteObjectIdentifier("2.16.840.1.101.3.4.2.1");
                writer.WriteOctetString(SHA256.HashData(stamped));
            }
            writer.WriteInteger(42);
            writer.WriteGeneralizedTime(moment, omitFractionalSeconds: true);
        }

        var cms = new SignedCms(new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), writer.Encode()));
        var signer = new CmsSigner(authority) { IncludeOption = X509IncludeOption.EndCertOnly };
        signer.SignedAttributes.Add(new AsnEncodedData(new Oid("1.2.840.113549.1.9.16.2.47"), SigningCertificate(authority)));
        cms.ComputeSignature(signer);
        return cms.Encode();
    }

    private byte[] SigningCertificate(X509Certificate2 authority)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        using (writer.PushSequence())
        using (writer.PushSequence())
            writer.WriteOctetString(SHA256.HashData(authority.RawData));
        return writer.Encode();
    }

    [Fact]
    public void Un_certificato_valido_oggi_senza_marca_da_una_firma_valida()
    {
        using var signer = Signer(DateTimeOffset.Now.AddYears(-1), DateTimeOffset.Now.AddYears(1));

        Assert.Equal(SignatureStatus.Valid, Judge(signer).Judge(signer));
    }

    [Fact]
    public void Un_certificato_scaduto_senza_marca_da_valida_ma_non_verificabile_nel_tempo()
    {
        using var signer = Signer(DateTimeOffset.Now.AddYears(-2), DateTimeOffset.Now.AddDays(-1));

        Assert.Equal(SignatureStatus.UnverifiableInTime, Judge(signer).Judge(signer));
    }

    [Fact]
    public void La_data_dichiarata_dentro_la_validita_non_basta_se_il_certificato_oggi_e_scaduto()
    {
        var parts = Parts.Sample();
        using var signer = Signer(DateTimeOffset.Now.AddYears(-2), DateTimeOffset.Now.AddDays(-1));
        var builder = new XmlSignatureBuilder(Judge(signer));

        var signatures = new XElement(XmlSignatureBuilder.Document + "document-signatures",
            builder.Sign(parts, signer, DateTimeOffset.Now.AddYears(-1)));

        var verified = Assert.Single(builder.Verify(signatures, parts));
        Assert.Equal(SignatureStatus.UnverifiableInTime, verified.Status);
        Assert.False(verified.IsTrusted);
    }

    [Fact]
    public void Con_una_marca_fidata_dentro_la_validita_la_firma_e_valida()
    {
        var parts = Parts.Sample();
        using var signer = Signer(DateTimeOffset.Now.AddYears(-2), DateTimeOffset.Now.AddDays(-1));
        var builder = new XmlSignatureBuilder(Judge(signer));
        var signature = builder.Sign(parts, signer, DateTimeOffset.Now.AddYears(-1));
        Stamp(signature, DateTimeOffset.Now.AddYears(-1));

        var verified = Assert.Single(builder.Verify(new XElement(XmlSignatureBuilder.Document + "document-signatures", signature), parts));

        Assert.Equal(SignatureStatus.Valid, verified.Status);
    }

    [Fact]
    public void Con_una_marca_fidata_dopo_la_scadenza_il_certificato_e_scaduto()
    {
        var parts = Parts.Sample();
        using var signer = Signer(DateTimeOffset.Now.AddYears(-2), DateTimeOffset.Now.AddDays(-10));
        var builder = new XmlSignatureBuilder(Judge(signer));
        var signature = builder.Sign(parts, signer, DateTimeOffset.Now.AddYears(-1));
        Stamp(signature, DateTimeOffset.Now.AddDays(-2));

        var verified = Assert.Single(builder.Verify(new XElement(XmlSignatureBuilder.Document + "document-signatures", signature), parts));

        Assert.Equal(SignatureStatus.ExpiredCertificate, verified.Status);
    }

    [Fact]
    public void Una_marca_su_altri_dati_non_conta()
    {
        using var signer = Signer(DateTimeOffset.Now.AddYears(-2), DateTimeOffset.Now.AddDays(-1));
        var evidence = new TimestampEvidence(Token([1, 2, 3], DateTimeOffset.Now.AddYears(-1)), [4, 5, 6]);

        Assert.Equal(SignatureStatus.UnverifiableInTime, Judge(signer).Judge(signer, evidence));
    }

    [Fact]
    public void Una_marca_di_unautorita_non_fidata_non_conta()
    {
        using var signer = Signer(DateTimeOffset.Now.AddYears(-2), DateTimeOffset.Now.AddDays(-1));
        var trust = new CustomRootCertificateTrust([signer]);
        var judge = new CertificateJudge(trust, new TrustedTimestampVerifier(trust), TimeProvider.System);
        var evidence = new TimestampEvidence(Token([1, 2, 3], DateTimeOffset.Now.AddYears(-1)), [1, 2, 3]);

        Assert.Equal(SignatureStatus.UnverifiableInTime, judge.Judge(signer, evidence));
    }

    [Fact]
    public void Una_marca_firmata_da_un_certificato_fidato_ma_non_per_le_marche_non_conta()
    {
        using var signer = Signer(DateTimeOffset.Now.AddYears(-2), DateTimeOffset.Now.AddDays(-1));
        using var other = Signer(DateTimeOffset.Now.AddYears(-1), DateTimeOffset.Now.AddYears(1));
        var evidence = new TimestampEvidence(Token([1, 2, 3], DateTimeOffset.Now.AddYears(-1), other), [1, 2, 3]);

        Assert.Equal(SignatureStatus.UnverifiableInTime, Judge(signer, other).Judge(signer, evidence));
    }

    [Fact]
    public void Una_marca_nel_futuro_secondo_l_orologio_iniettato_non_conta()
    {
        using var signer = Signer(DateTimeOffset.Now.AddYears(-2), DateTimeOffset.Now.AddDays(-1));
        var trust = new CustomRootCertificateTrust([signer, _authority]);
        var past = new FixedClock(DateTimeOffset.Now.AddYears(-3));
        var judge = new CertificateJudge(trust, new TrustedTimestampVerifier(trust, past), past);
        var evidence = new TimestampEvidence(Token([1, 2, 3], DateTimeOffset.Now.AddYears(-1)), [1, 2, 3]);

        Assert.Equal(SignatureStatus.ExpiredCertificate, judge.Judge(signer, evidence));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }

    private void Stamp(XElement signature, DateTimeOffset moment)
    {
        XNamespace xades = "http://uri.etsi.org/01903/v1.3.2#";
        var value = signature.Element(XmlSignatureBuilder.Signature + "SignatureValue")!;
        var token = Token(new XmlDsigAlgorithms().Canonical(value), moment);
        signature.Add(new XElement(XmlSignatureBuilder.Signature + "Object",
            new XElement(xades + "QualifyingProperties",
                new XElement(xades + "UnsignedProperties",
                    new XElement(xades + "UnsignedSignatureProperties",
                        new XElement(xades + "SignatureTimeStamp",
                            new XElement(xades + "EncapsulatedTimeStamp", Convert.ToBase64String(token))))))));
    }
}

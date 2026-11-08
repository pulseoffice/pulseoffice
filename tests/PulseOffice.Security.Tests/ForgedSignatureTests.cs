using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Xunit;

namespace PulseOffice.Security.Tests;

public sealed class ForgedSignatureTests
{
    private static readonly XNamespace Ds = XmlSignatureBuilder.Signature;

    private readonly XmlSignatureBuilder _builder = new();

    private X509Certificate2 Certificate(string name = "CN=Autore", DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(from ?? DateTimeOffset.Now.AddDays(-1), to ?? DateTimeOffset.Now.AddDays(365));
    }

    private XElement Signed(Parts parts, X509Certificate2 certificate, DateTimeOffset? signedOn = null) =>
        new(XmlSignatureBuilder.Document + "document-signatures", _builder.Sign(parts, certificate, signedOn ?? DateTimeOffset.Now));

    private SignatureStatus Status(XElement signatures, Parts parts) => Assert.Single(_builder.Verify(signatures, parts)).Status;

    [Fact]
    public void A_date_added_outside_the_signed_properties_does_not_change_the_signing_date()
    {
        var parts = Parts.Sample();
        using var signer = Certificate();
        var signedOn = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var signatures = Signed(parts, signer, signedOn);
        var signature = signatures.Element(Ds + "Signature")!;

        signature.Element(Ds + "SignedInfo")!.AddAfterSelf(new XElement(Ds + "Object",
            new XElement(Ds + "SignedInfo", new XElement(Ds + "Reference", new XAttribute("URI", "#forged"))),
            new XElement(Ds + "SignatureProperty", new XAttribute("Id", "forged"),
                new XElement(XmlSignatureBuilder.Dc + "date", "2001-01-01T00:00:00"))));

        var read = Assert.Single(_builder.Verify(signatures, parts));
        Assert.Equal(signedOn.Year, read.SignedOn.Year);
    }

    [Fact]
    public void Swapping_in_another_certificate_breaks_the_signature()
    {
        var parts = Parts.Sample();
        using var signer = Certificate();
        using var impostor = Certificate("CN=Banca");
        var signatures = Signed(parts, signer);

        signatures.Descendants(Ds + "X509Certificate").Single().Value = Convert.ToBase64String(impostor.RawData);

        Assert.Equal(SignatureStatus.Broken, Status(signatures, parts));
    }

    [Fact]
    public void Adding_a_second_certificate_does_not_let_the_signer_change_name()
    {
        var parts = Parts.Sample();
        using var signer = Certificate();
        using var impostor = Certificate("CN=Banca");
        var signatures = Signed(parts, signer);

        signatures.Descendants(Ds + "X509Data").Single().AddFirst(new XElement(Ds + "X509Certificate", Convert.ToBase64String(impostor.RawData)));

        var signature = Assert.Single(_builder.Verify(signatures, parts));
        Assert.NotEqual(SignatureStatus.Valid, signature.Status);
        Assert.Equal("Autore", signature.Signer.Name);
    }

    [Fact]
    public void A_wrapped_copy_of_the_signed_properties_is_not_accepted()
    {
        var parts = Parts.Sample();
        using var signer = Certificate();
        var signatures = Signed(parts, signer);
        var property = signatures.Descendants(Ds + "SignatureProperty").Single();
        var original = new XElement(property);
        property.Element(XmlSignatureBuilder.Dc + "date")!.Value = "2001-01-01T00:00:00";
        signatures.Descendants(Ds + "Signature").Single().Add(new XElement(Ds + "Object", new XElement("Wrapper", original)));

        Assert.Equal(SignatureStatus.Broken, Status(signatures, parts));
    }

    [Fact]
    public void A_second_signed_info_slipped_in_front_is_not_accepted()
    {
        var parts = Parts.Sample();
        using var signer = Certificate();
        var signatures = Signed(parts, signer);
        var signature = signatures.Descendants(Ds + "Signature").Single();
        var signedInfo = signature.Element(Ds + "SignedInfo")!;
        var forged = new XElement(signedInfo);
        forged.Elements(Ds + "Reference").First(reference => (string?)reference.Attribute("URI") == "content.xml").Element(Ds + "DigestValue")!.Value =
            Convert.ToBase64String(SHA256.HashData("<office:document-content>falso</office:document-content>"u8.ToArray()));
        signature.AddFirst(forged);
        parts.Replace("content.xml", "<office:document-content>falso</office:document-content>");

        Assert.Equal(SignatureStatus.Broken, Status(signatures, parts));
    }

    [Fact]
    public void A_reference_with_a_foreign_transform_is_not_accepted()
    {
        var parts = Parts.Sample();
        using var signer = Certificate();
        var signatures = Signed(parts, signer);
        var reference = signatures.Descendants(Ds + "Reference").First(candidate => (string?)candidate.Attribute("URI") == "content.xml");
        reference.AddFirst(new XElement(Ds + "Transforms", new XElement(Ds + "Transform", new XAttribute("Algorithm", "http://www.w3.org/TR/1999/REC-xslt-19991116"))));

        Assert.Equal(SignatureStatus.Broken, Status(signatures, parts));
    }

    [Fact]
    public void Duplicating_a_reference_to_hide_a_removed_part_is_not_accepted()
    {
        var parts = Parts.Sample();
        using var signer = Certificate();
        var signatures = Signed(parts, signer);
        var references = signatures.Descendants(Ds + "Reference").ToList();
        var styles = references.First(candidate => (string?)candidate.Attribute("URI") == "styles.xml");
        styles.ReplaceWith(new XElement(references.First(candidate => (string?)candidate.Attribute("URI") == "content.xml")));

        Assert.Equal(SignatureStatus.Broken, Status(signatures, parts));
    }

    [Fact]
    public void A_signature_from_another_document_does_not_validate_this_one()
    {
        var other = Parts.Sample();
        other.Replace("content.xml", "<office:document-content>altro</office:document-content>");
        using var signer = Certificate();

        Assert.Equal(SignatureStatus.Broken, Status(Signed(other, signer), Parts.Sample()));
    }

    [Fact]
    public void A_signature_made_with_an_expired_and_untrusted_certificate_is_not_trusted()
    {
        var parts = Parts.Sample();
        using var signer = Certificate(from: DateTimeOffset.Now.AddYears(-3), to: DateTimeOffset.Now.AddYears(-2));

        Assert.Equal(SignatureStatus.UntrustedCertificate, Status(Signed(parts, signer), parts));
    }

    [Fact]
    public void A_signature_dated_before_the_certificate_existed_is_not_valid()
    {
        var parts = Parts.Sample();
        using var signer = Certificate(from: DateTimeOffset.Now.AddDays(10), to: DateTimeOffset.Now.AddYears(1));

        Assert.Equal(SignatureStatus.ExpiredCertificate, Status(Signed(parts, signer, DateTimeOffset.Now), parts));
    }

    [Fact]
    public void Removing_the_signature_value_breaks_the_signature()
    {
        var parts = Parts.Sample();
        using var signer = Certificate();
        var signatures = Signed(parts, signer);
        signatures.Descendants(Ds + "SignatureValue").Single().Value = string.Empty;

        Assert.Equal(SignatureStatus.Broken, Status(signatures, parts));
    }
}

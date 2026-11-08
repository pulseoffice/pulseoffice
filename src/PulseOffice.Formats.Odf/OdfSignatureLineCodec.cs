using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Units;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

public sealed class OdfSignatureLineCodec(SignatureLineImage image)
{
    public const string MediaType = "image/svg+xml";

    private readonly XNamespace _pulse = "urn:pulseoffice:signature";

    public XName ElementName => Namespaces.Loext + "signatureline";

    public bool Holds(XElement frame) => frame.Element(ElementName) is not null;

    public XElement Element(SignatureLineObject line) =>
        new(ElementName,
            new XAttribute(Namespaces.Loext + "id", line.Id),
            Optional(Namespaces.Loext + "suggested-signer-name", line.SignerName),
            Optional(Namespaces.Loext + "suggested-signer-title", line.SignerTitle),
            Optional(Namespaces.Loext + "suggested-signer-email", line.SignerEmail),
            Optional(Namespaces.Loext + "signing-instructions", line.Instructions),
            new XAttribute(Namespaces.Loext + "show-sign-date", line.ShowDate ? "true" : "false"),
            new XAttribute(Namespaces.Loext + "can-add-comment", line.AllowComments ? "true" : "false"),
            line.SignedBy is not null || line.SignedOn is not null ? new XAttribute(XNamespace.Xmlns + "pulsesig", _pulse) : null,
            Optional(_pulse + "signed-by", line.SignedBy),
            line.SignedOn is { } signedOn ? new XAttribute(_pulse + "signed-on", signedOn.ToString("O", CultureInfo.InvariantCulture)) : null);

    public SignatureLineObject? Read(XElement frame, string fallbackId)
    {
        if (frame.Element(ElementName) is not { } element) return null;
        return new SignatureLineObject
        {
            Id = (string?)element.Attribute(Namespaces.Loext + "id") is { Length: > 0 } id ? id : fallbackId,
            NaturalSize = SignatureLineObject.StandardSize,
            SignerName = Text(element, Namespaces.Loext + "suggested-signer-name"),
            SignerTitle = Text(element, Namespaces.Loext + "suggested-signer-title"),
            SignerEmail = Text(element, Namespaces.Loext + "suggested-signer-email"),
            Instructions = Text(element, Namespaces.Loext + "signing-instructions"),
            ShowDate = (string?)element.Attribute(Namespaces.Loext + "show-sign-date") == "true",
            AllowComments = (string?)element.Attribute(Namespaces.Loext + "can-add-comment") == "true",
            SignedBy = Text(element, _pulse + "signed-by"),
            SignedOn = DateTimeOffset.TryParse((string?)element.Attribute(_pulse + "signed-on"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var on) ? on : null,
            Title = (string?)frame.Element(Namespaces.Svg + "title"),
            Description = (string?)frame.Element(Namespaces.Svg + "desc"),
        };
    }

    public byte[] UnsignedImage(SignatureLineObject line, DocumentSize size) => image.Unsigned(line, size);

    public byte[]? UnsignedRaster(SignatureLineObject line, DocumentSize size) => image.UnsignedRaster(line, size);

    public string RasterMediaType => image.RasterMediaType;

    public XElement Fallback(string path) =>
        new(Namespaces.Draw + "image",
            new XAttribute(Namespaces.Xlink + "href", path),
            new XAttribute(Namespaces.Xlink + "type", "simple"),
            new XAttribute(Namespaces.Xlink + "show", "embed"),
            new XAttribute(Namespaces.Xlink + "actuate", "onLoad"),
            new XAttribute(Namespaces.Draw + "mime-type", image.RasterMediaType));

    private XAttribute? Optional(XName name, string? value) =>
        string.IsNullOrEmpty(value) ? null : new XAttribute(name, value);

    private string? Text(XElement element, XName name) =>
        (string?)element.Attribute(name) is { Length: > 0 } value ? value : null;
}

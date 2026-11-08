using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats.OpenXml;

public sealed class VmlSignatureLineCodec(SignatureLineImage image)
{
    private const string Provider = "{00000000-0000-0000-0000-000000000000}";

    public XNamespace Vml { get; } = "urn:schemas-microsoft-com:vml";

    public XNamespace Office { get; } = "urn:schemas-microsoft-com:office:office";

    public XNamespace Relationships { get; } = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public string ImageMediaType => image.MediaType;

    public byte[] UnsignedImage(SignatureLineObject line, DocumentSize size) => image.Unsigned(line, size);

    public byte[]? UnsignedRaster(SignatureLineObject line, DocumentSize size) => image.UnsignedRaster(line, size);

    public XElement PictureType() =>
        new(Vml + "shapetype",
            new XAttribute("id", "_x0000_t75"),
            new XAttribute("coordsize", "21600,21600"),
            new XAttribute(Office + "spt", "75"),
            new XAttribute(Office + "preferrelative", "t"),
            new XAttribute("filled", "f"),
            new XAttribute("stroked", "f"),
            new XElement(Vml + "stroke", new XAttribute("joinstyle", "miter")),
            new XElement(Vml + "path", new XAttribute(Office + "extrusionok", "f"), new XAttribute("gradientshapeok", "t"), new XAttribute(Office + "connecttype", "rect")),
            new XElement(Office + "lock", new XAttribute(Vml + "ext", "edit"), new XAttribute("aspectratio", "t")));

    public XElement Shape(SignatureLineObject line, string shapeId, string style, XAttribute imageReference) =>
        new(Vml + "shape",
            new XAttribute("id", shapeId),
            new XAttribute("type", "#_x0000_t75"),
            line.Description is { Length: > 0 } description ? new XAttribute("alt", description) : null,
            line.Title is { Length: > 0 } title ? new XAttribute(Office + "title", title) : null,
            new XAttribute("style", style),
            new XElement(Vml + "imagedata", imageReference, new XAttribute(Office + "title", string.Empty)),
            new XElement(Office + "lock", new XAttribute(Vml + "ext", "edit"), new XAttribute("ungrouping", "t"), new XAttribute("rotation", "t"),
                new XAttribute("cropping", "t"), new XAttribute("verticies", "t"), new XAttribute("text", "t"), new XAttribute("grouping", "t")),
            Element(line));

    public XElement Element(SignatureLineObject line) =>
        new(Office + "signatureline",
            new XAttribute(Vml + "ext", "edit"),
            new XAttribute("id", Identifier(line.Id)),
            new XAttribute("provid", Provider),
            Optional(Office + "suggestedsigner", line.SignerName),
            Optional(Office + "suggestedsigner2", line.SignerTitle),
            Optional(Office + "suggestedsigneremail", line.SignerEmail),
            line.Instructions is { Length: > 0 } ? new XAttribute("signinginstructionsset", "t") : null,
            Optional("signinginstructions", line.Instructions),
            new XAttribute("showsigndate", line.ShowDate ? "t" : "f"),
            new XAttribute("allowcomments", line.AllowComments ? "t" : "f"),
            new XAttribute("issignatureline", "t"));

    public bool Holds(XElement shape) => shape.Element(Office + "signatureline") is not null;

    public SignatureLineObject? Read(XElement shape, string fallbackId)
    {
        if (shape.Element(Office + "signatureline") is not { } element) return null;
        return new SignatureLineObject
        {
            Id = (string?)element.Attribute("id") is { Length: > 0 } id ? id : fallbackId,
            NaturalSize = SignatureLineObject.StandardSize,
            SignerName = Text(element, Office + "suggestedsigner"),
            SignerTitle = Text(element, Office + "suggestedsigner2"),
            SignerEmail = Text(element, Office + "suggestedsigneremail"),
            Instructions = Text(element, "signinginstructions") ?? Text(element, Office + "signinginstructions"),
            ShowDate = Flag(element, "showsigndate", true),
            AllowComments = Flag(element, "allowcomments", false),
            Title = Text(shape, Office + "title"),
            Description = Text(shape, "alt"),
        };
    }

    private string Identifier(string id)
    {
        if (Guid.TryParse(id, out var parsed)) return parsed.ToString("B").ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(id));
        return new Guid(hash.AsSpan(0, 16)).ToString("B").ToUpperInvariant();
    }

    private bool Flag(XElement element, XName name, bool fallback) =>
        (string?)element.Attribute(name) is { Length: > 0 } value ? value is "t" or "true" or "1" : fallback;

    private XAttribute? Optional(XName name, string? value) =>
        string.IsNullOrEmpty(value) ? null : new XAttribute(name, value);

    private string? Text(XElement element, XName name) =>
        (string?)element.Attribute(name) is { Length: > 0 } value ? value : null;
}

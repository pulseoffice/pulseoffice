using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Metadata;

namespace PulseOffice.Formats.Odf;

public sealed class OdfMetaMapper
{
    private static readonly XNamespace Office = OdfPackage.Namespaces.Office;
    private static readonly XNamespace Meta = OdfPackage.Namespaces.Meta;
    private static readonly XNamespace Dc = OdfPackage.Namespaces.Dc;

    private readonly TokenMap<CustomPropertyKind> _kinds = new(CustomPropertyKind.Text, "string",
        (CustomPropertyKind.Text, "string"), (CustomPropertyKind.Number, "float"),
        (CustomPropertyKind.Boolean, "boolean"), (CustomPropertyKind.Date, "date"));

    public XDocument Build(
        DocumentProperties properties,
        string? fallbackTitle = null,
        string? fallbackAuthor = null,
        DateTimeOffset? fallbackCreated = null)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var created = properties.Created ?? fallbackCreated ?? DateTimeOffset.Now;

        var meta = new XElement(Office + "meta",
            new XElement(Meta + "generator", properties.Generator ?? "PulseOffice"),
            new XElement(Meta + "creation-date", Moment(created)));

        if ((properties.Title ?? fallbackTitle) is { } title) meta.Add(new XElement(Dc + "title", title));
        if (properties.Subject is { } subject) meta.Add(new XElement(Dc + "subject", subject));
        if (properties.Description is { } description) meta.Add(new XElement(Dc + "description", description));
        if ((properties.Author ?? fallbackAuthor) is { } author) meta.Add(new XElement(Meta + "initial-creator", author));
        if (properties.LastModifiedBy is { } editor) meta.Add(new XElement(Dc + "creator", editor));
        if (properties.Modified is { } modified) meta.Add(new XElement(Dc + "date", Moment(modified)));
        if (properties.Language is { } language) meta.Add(new XElement(Dc + "language", language));

        foreach (var keyword in properties.Keywords)
            meta.Add(new XElement(Meta + "keyword", keyword));

        if (properties.EditingCycles > 0)
            meta.Add(new XElement(Meta + "editing-cycles",
                properties.EditingCycles.ToString(CultureInfo.InvariantCulture)));

        foreach (var (name, value) in properties.Custom)
            meta.Add(new XElement(Meta + "user-defined", new XAttribute(Meta + "name", name),
                new XAttribute(Meta + "value-type", _kinds.Token(properties.KindOf(name))), value));

        return new XDocument(new XElement(Office + "document-meta",
            new XAttribute(XNamespace.Xmlns + "office", Office.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "meta", Meta.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName),
            new XAttribute(Office + "version", "1.3"),
            meta));
    }

    public void Read(XDocument? document, DocumentProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (document?.Root?.Element(Office + "meta") is not { } meta) return;

        properties.Title = Value(meta, Dc + "title");
        properties.Subject = Value(meta, Dc + "subject");
        properties.Description = Value(meta, Dc + "description");
        properties.Author = Value(meta, Meta + "initial-creator");
        properties.LastModifiedBy = Value(meta, Dc + "creator");
        properties.Generator = Value(meta, Meta + "generator");
        properties.Language = Value(meta, Dc + "language");

        if (Moment(Value(meta, Meta + "creation-date")) is { } created) properties.Created = created;
        if (Moment(Value(meta, Dc + "date")) is { } modified) properties.Modified = modified;

        if (int.TryParse(Value(meta, Meta + "editing-cycles"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var cycles) && cycles >= 0)
            properties.EditingCycles = cycles;

        properties.Keywords.Clear();
        foreach (var keyword in meta.Elements(Meta + "keyword"))
            properties.Keywords.Add(keyword.Value);

        properties.Custom.Clear();
        properties.CustomKinds.Clear();
        foreach (var custom in meta.Elements(Meta + "user-defined"))
            if (custom.Attribute(Meta + "name")?.Value is { Length: > 0 } name)
            {
                properties.Custom[name] = custom.Value;
                var kind = _kinds.Value(custom.Attribute(Meta + "value-type")?.Value);
                if (kind == CustomPropertyKind.Text) properties.CustomKinds.Remove(name);
                else properties.CustomKinds[name] = kind;
            }
    }

    private string? Value(XElement parent, XName name) =>
        parent.Element(name)?.Value is { Length: > 0 } text ? text : null;

    private string Moment(DateTimeOffset value) =>
        value.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private DateTimeOffset? Moment(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : null;
}

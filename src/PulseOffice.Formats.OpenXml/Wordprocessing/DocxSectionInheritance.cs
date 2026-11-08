using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxSectionInheritance
{
    private readonly Dictionary<string, string> _headers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _footers = new(StringComparer.Ordinal);

    private string _signature = string.Empty;

    public bool Changed { get; private set; }

    public W.SectionProperties Resolve(W.SectionProperties section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var resolved = (W.SectionProperties)section.CloneNode(true);
        Inherit(resolved, resolved.Elements<W.HeaderReference>().Select(reference => (Kind(reference.Type), reference.Id?.Value)).ToList(), _headers,
            (kind, id) => new W.HeaderReference { Type = Type(kind), Id = id });
        Inherit(resolved, resolved.Elements<W.FooterReference>().Select(reference => (Kind(reference.Type), reference.Id?.Value)).ToList(), _footers,
            (kind, id) => new W.FooterReference { Type = Type(kind), Id = id });
        var signature = string.Join(";", _headers.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => "h" + pair.Key + "=" + pair.Value)
            .Concat(_footers.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => "f" + pair.Key + "=" + pair.Value)));
        Changed = signature != _signature;
        _signature = signature;
        return resolved;
    }

    private void Inherit(W.SectionProperties section, List<(string Kind, string? Id)> own, Dictionary<string, string> inherited, Func<string, string, DocumentFormat.OpenXml.OpenXmlElement> reference)
    {
        foreach (var (kind, id) in own)
        {
            if (id is not null) inherited[kind] = id;
        }

        foreach (var (kind, id) in inherited.Where(pair => own.All(item => item.Kind != pair.Key)).ToList())
            section.PrependChild(reference(kind, id));
    }

    private string Kind(DocumentFormat.OpenXml.EnumValue<W.HeaderFooterValues>? type) =>
        type?.Value == W.HeaderFooterValues.First ? "first" : type?.Value == W.HeaderFooterValues.Even ? "even" : "default";

    private W.HeaderFooterValues Type(string kind) =>
        kind == "first" ? W.HeaderFooterValues.First : kind == "even" ? W.HeaderFooterValues.Even : W.HeaderFooterValues.Default;
}

using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpSounds(SoundTypes types)
{
    private const string PackageScheme = "vnd.sun.star.Package:";
    private readonly XName _href = OdfPackage.Namespaces.Xlink + "href";
    private readonly HashSet<XName> _carriers = [OdfPackage.Namespaces.Presentation + "sound", OdfPackage.Namespaces.Anim + "audio"];

    public void Unpack(XDocument? document)
    {
        foreach (var attribute in Hrefs(document))
            if (attribute.Value.StartsWith(PackageScheme, StringComparison.Ordinal))
                attribute.Value = attribute.Value[PackageScheme.Length..];
    }

    public void Read(Presentation deck, Func<string, byte[]?> entry)
    {
        foreach (var path in deck.Sounds.Referenced(deck).ToList())
            if (Inside(path) && entry(path) is { Length: > 0 } data)
                deck.Sounds.Add(new EmbeddedSound(path, types.MimeTypeOf(path), data));
    }

    public void Write(OdpWriteSession session, params XDocument[] documents)
    {
        foreach (var path in session.Deck.Sounds.Referenced(session.Deck))
            if (session.Deck.Sounds.Find(path) is { } sound)
                session.AddMedia(sound.Path, sound.Data, sound.MimeType);
        foreach (var attribute in documents.SelectMany(Hrefs))
            if (session.Deck.Sounds.Find(attribute.Value) is not null)
                attribute.Value = PackageScheme + attribute.Value;
    }

    private IEnumerable<XAttribute> Hrefs(XDocument? document) =>
        document?.Descendants().Where(element => _carriers.Contains(element.Name)).Select(element => element.Attribute(_href)).OfType<XAttribute>().ToList() ?? [];

    private bool Inside(string path) =>
        !path.StartsWith("../", StringComparison.Ordinal) && !path.StartsWith('/') && !Uri.TryCreate(path, UriKind.Absolute, out _);
}

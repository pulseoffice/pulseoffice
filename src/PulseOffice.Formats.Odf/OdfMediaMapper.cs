using System.Xml.Linq;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

public sealed class OdfMediaMapper(ILinkedFiles links, OdfMediaPlayback playback)
{
    public OdfMediaMapper() : this(new LinkedFiles(), new OdfMediaPlayback())
    {
    }

    private readonly OdfHrefs _hrefs = new();
    private readonly MediaTypes _types = new();

    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;
    private static readonly XNamespace Xlink = OdfPackage.Namespaces.Xlink;
    private static readonly XNamespace Xml = OdfPackage.Namespaces.Xml;

    private readonly XNamespace _pulse = "urn:pulseoffice:media";

    public string LinkedHref(MediaElement media) => links.ToPackageHref(media.LinkedPath!);

    public XElement BuildFrame(MediaElement media, string href, string? posterHref = null)
    {
        var frame = new XElement(Draw + "frame",
            new XAttribute(Xml + "id", media.Id),
            new XAttribute(Draw + "id", media.Id),
            new XAttribute(Svg + "x", Cm(media.Bounds.Left)),
            new XAttribute(Svg + "y", Cm(media.Bounds.Top)),
            new XAttribute(Svg + "width", Cm(media.Bounds.Width)),
            new XAttribute(Svg + "height", Cm(media.Bounds.Height)),
            new XElement(Draw + "plugin",
                new XAttribute(Xlink + "href", href),
                new XAttribute(Xlink + "type", "simple"),
                new XAttribute(Xlink + "show", "embed"),
                new XAttribute(Xlink + "actuate", "onLoad"),
                new XAttribute(Draw + "mime-type", MediaTypes.StarMediaType),
                playback.Write(media)));
        if (media.FileName is { Length: > 0 } name)
            frame.Add(new XAttribute(XNamespace.Xmlns + "pulsemedia", _pulse.NamespaceName), new XAttribute(_pulse + "file-name", name));
        if (posterHref is not null)
            frame.Add(new XElement(Draw + "image",
                new XAttribute(Xlink + "href", posterHref),
                new XAttribute(Xlink + "type", "simple"),
                new XAttribute(Xlink + "show", "embed"),
                new XAttribute(Xlink + "actuate", "onLoad")));

        return frame;
    }

    public MediaElement? Read(XElement frame, string id, DocumentRect bounds, int zOrder, Func<string, byte[]?> readBytes)
    {
        var plugin = frame.Element(Draw + "plugin");
        if (plugin is null) return null;

        var href = (string?)plugin.Attribute(Xlink + "href");
        if (href is null) return null;

        var path = _hrefs.Normalize(href);
        var linked = links.IsExternal(href) ? links.FromPackageHref(href) : null;
        var data = linked is null ? readBytes(path) : [];
        if (data is null) return null;

        var mime = _types.Resolve((string?)plugin.Attribute(Draw + "mime-type"), linked ?? path, data);
        var settings = playback.Read(plugin);

        return new MediaElement
        {
            Id = id,
            Bounds = bounds,
            ZOrder = zOrder,
            Kind = _types.IsAudio(mime) ? MediaKind.Audio : MediaKind.Video,
            Data = data,
            MimeType = mime,
            FileName = (string?)frame.Attribute(_pulse + "file-name"),
            PosterData = frame.Element(Draw + "image")?.Attribute(Xlink + "href")?.Value is { Length: > 0 } poster ? readBytes(_hrefs.Normalize(poster)) : null,
            LinkedPath = linked,
            Loop = settings.Loop,
            Muted = settings.Muted,
            Volume = settings.Volume,
        };
    }

    private string Cm(Twip value) =>
        (value.Value / 566.9291).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "cm";
}

public sealed record MediaPlayback(bool Loop, bool Muted, double Volume);

public sealed class OdfMediaPlayback
{
    private const double SilentDecibels = -40;

    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    public IEnumerable<XElement> Write(MediaElement media)
    {
        yield return Parameter("Loop", media.Loop ? "true" : "false");
        yield return Parameter("Mute", media.Muted ? "true" : "false");
        yield return Parameter("VolumeDB", Decibels(media.Volume).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public MediaPlayback Read(XElement plugin)
    {
        var values = plugin.Elements(Draw + "param")
            .Where(parameter => parameter.Attribute(Draw + "name") is not null)
            .GroupBy(parameter => (string)parameter.Attribute(Draw + "name")!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (string?)group.First().Attribute(Draw + "value"), StringComparer.Ordinal);
        var volume = values.TryGetValue("VolumeDB", out var decibels)
            && double.TryParse(decibels, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && !double.IsNaN(parsed)
                ? Fraction(parsed) : 1;
        return new MediaPlayback(Flag(values, "Loop"), Flag(values, "Mute"), volume);
    }

    private XElement Parameter(string name, string value) =>
        new(Draw + "param", new XAttribute(Draw + "name", name), new XAttribute(Draw + "value", value));

    private bool Flag(Dictionary<string, string?> values, string name) =>
        values.TryGetValue(name, out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private int Decibels(double volume) =>
        volume <= 0 ? (int)SilentDecibels : (int)Math.Round(Math.Clamp(20 * Math.Log10(Math.Min(1, volume)), SilentDecibels, 0));

    private double Fraction(double decibels) =>
        decibels <= SilentDecibels ? 0 : Math.Round(Math.Pow(10, Math.Min(0, decibels) / 20), 4);
}

using System.Text;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing;
using PulseOffice.Drawing.Assets;
using PulseOffice.Sheets;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class SheetImageVisitor(
    PlacedObject placed, List<(string Path, string MediaType, byte[] Content)> pictures,
    OdfImageTypes types, VectorFallback vectors, OdfDrawUnits units, OdfDrawRotation rotation, OdfImageMirrors mirrors)
    : DrawingObjectVisitor<XElement?>
{
    public override XElement? VisitRaster(RasterImageObject raster)
    {
        var frame = Frame(raster.LinkPath is { Length: > 0 } linked
            ? new PulseOffice.Formats.LinkedFiles().ToPackageHref(new PulseOffice.Formats.LinkedFiles().ToHref(linked))
            : Store($".{types.Extension(raster.MediaType)}", raster.MediaType, raster.Data.ToArray()), 0, false, false);
        if (mirrors.StyleFor(raster, placed.Size) is { } look) frame.SetAttributeValue(Namespaces.Draw + "style-name", look);
        if (new OdfImageMapCodec().Build(raster.Map, placed.Size) is { } map) frame.Add(map);
        return frame;
    }

    public override XElement? VisitVector(VectorGraphicObject vector)
    {
        if (string.IsNullOrEmpty(vector.Asset.SvgContent)) return null;
        var svg = vectors.Svg(vector);
        var frame = Frame(Store(".svg", vectors.MediaType, svg), vector.RotationDegrees, vector.FlipHorizontal, vector.FlipVertical);
        var image = frame.Element(Namespaces.Draw + "image")!;
        image.SetAttributeValue(Namespaces.Draw + "mime-type", vectors.MediaType);
        var fallback = new XElement(image);
        fallback.SetAttributeValue(Namespaces.Xlink + "href", Store(".png", vectors.FallbackMediaType, vectors.Png(svg, placed.Size)));
        fallback.SetAttributeValue(Namespaces.Draw + "mime-type", vectors.FallbackMediaType);
        image.AddAfterSelf(fallback);
        return frame;
    }

    protected override XElement? Otherwise(EmbeddedObject content) => null;

    private string Store(string extension, string mediaType, byte[] content)
    {
        var path = $"Pictures/{pictures.Count + 1:0000}{extension}";
        pictures.Add((path, mediaType, content));
        return path;
    }

    private XElement Frame(string path, double degrees, bool flipHorizontal, bool flipVertical)
    {
        var frame = new XElement(Namespaces.Draw + "frame",
            new XAttribute(Namespaces.Draw + "name", placed.Id),
            new XAttribute(Namespaces.Draw + "z-index", placed.ZOrder.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new XAttribute(Namespaces.Svg + "width", units.Length(placed.Size.Width)),
            new XAttribute(Namespaces.Svg + "height", units.Length(placed.Size.Height)),
            new XElement(Namespaces.Draw + "image",
                new XAttribute(Namespaces.Xlink + "href", path),
                new XAttribute(Namespaces.Xlink + "type", "simple"),
                new XAttribute(Namespaces.Xlink + "show", "embed"),
                new XAttribute(Namespaces.Xlink + "actuate", "onLoad")));
        if (mirrors.StyleFor(flipHorizontal, flipVertical) is { } style) frame.SetAttributeValue(Namespaces.Draw + "style-name", style);
        rotation.Place(frame, placed.OffsetX, placed.OffsetY, placed.Size.Width, placed.Size.Height, degrees);
        return frame;
    }
}

internal sealed class OdfImageMirrors
{
    private readonly List<XElement> _styles = [];
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private readonly OdfImageTone _tones = new();
    private readonly OdfImageClip _clip = new(new PulseOffice.Formats.Odf.Odt.OdtMeasure());

    public IReadOnlyList<XElement> Declarations => _styles;

    public string? StyleFor(RasterImageObject raster, PulseOffice.Foundation.Units.DocumentSize frame)
    {
        ArgumentNullException.ThrowIfNull(raster);
        var properties = new XElement(Namespaces.Style + "graphic-properties");
        if (raster.Filter == ImageFilter.Grayscale) properties.SetAttributeValue(Namespaces.Draw + "color-mode", "greyscale");
        _tones.Write(properties, raster.Tone);
        if (raster.Crop != default) properties.SetAttributeValue(Namespaces.Fo + "clip", _clip.Write(raster, frame));
        if (!properties.HasAttributes) return null;

        var token = properties.ToString(SaveOptions.DisableFormatting);
        if (_names.TryGetValue(token, out var known)) return known;
        var name = $"gim{_styles.Count + 1}";
        _styles.Add(new XElement(Namespaces.Style + "style",
            new XAttribute(Namespaces.Style + "name", name),
            new XAttribute(Namespaces.Style + "family", "graphic"),
            properties));
        _names[token] = name;
        return name;
    }

    public RasterImageObject Look(XElement frame, XDocument? content, RasterImageObject raster, PulseOffice.Foundation.Units.DocumentSize size)
    {
        ArgumentNullException.ThrowIfNull(raster);
        if (Properties(frame, content) is not { } graphic) return raster;
        return raster with
        {
            Filter = (string?)graphic.Attribute(Namespaces.Draw + "color-mode") == "greyscale" ? ImageFilter.Grayscale : raster.Filter,
            Tone = _tones.Read(graphic),
            Crop = _clip.Read((string?)graphic.Attribute(Namespaces.Fo + "clip"), raster, size),
        };
    }

    private XElement? Properties(XElement frame, XDocument? content)
    {
        var name = (string?)frame.Attribute(Namespaces.Draw + "style-name");
        if (name is null || content is null) return null;
        return content.Descendants(Namespaces.Style + "style")
            .FirstOrDefault(style => (string?)style.Attribute(Namespaces.Style + "name") == name)
            ?.Element(Namespaces.Style + "graphic-properties");
    }

    public string? StyleFor(bool horizontal, bool vertical)
    {
        if (Token(horizontal, vertical) is not { } token) return null;
        if (_names.TryGetValue(token, out var known)) return known;

        var name = $"gim{_styles.Count + 1}";
        _styles.Add(new XElement(Namespaces.Style + "style",
            new XAttribute(Namespaces.Style + "name", name),
            new XAttribute(Namespaces.Style + "family", "graphic"),
            new XElement(Namespaces.Style + "graphic-properties", new XAttribute(Namespaces.Style + "mirror", token))));
        _names[token] = name;
        return name;
    }

    public (bool Horizontal, bool Vertical) Read(XElement frame, XDocument? content)
    {
        var name = (string?)frame.Attribute(Namespaces.Draw + "style-name");
        if (name is null || content is null) return (false, false);

        var mirror = content.Descendants(Namespaces.Style + "style")
            .FirstOrDefault(style => (string?)style.Attribute(Namespaces.Style + "name") == name)
            ?.Element(Namespaces.Style + "graphic-properties")?.Attribute(Namespaces.Style + "mirror")?.Value ?? string.Empty;
        var tokens = mirror.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return (tokens.Any(token => token.StartsWith("horizontal", StringComparison.Ordinal)), tokens.Contains("vertical"));
    }

    private string? Token(bool horizontal, bool vertical) => (horizontal, vertical) switch
    {
        (true, true) => "horizontal vertical",
        (true, false) => "horizontal",
        (false, true) => "vertical",
        _ => null,
    };
}

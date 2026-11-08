using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

public sealed class OoxmlPendingImages
{
    private const string Uri = "urn:pulseoffice:pending-image";
    private const string Placeholder = "rIdPulsePending";
    private const string Namespace = "urn:pulseoffice:image";

    private readonly PulseOffice.Drawing.Assets.ImageHeader _header = new();

    public A.Blip Blip(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var extension = new A.BlipExtension { Uri = Uri };
        extension.SetAttribute(new OpenXmlAttribute("pulse", "data", Namespace, Convert.ToBase64String(data)));
        return new A.Blip(new A.BlipExtensionList(extension)) { Embed = Placeholder };
    }

    public byte[]? Data(A.Blip blip)
    {
        ArgumentNullException.ThrowIfNull(blip);
        if (Extension(blip) is not { } extension) return null;
        try
        {
            return extension.GetAttributes().FirstOrDefault(attribute => attribute.NamespaceUri == Namespace && attribute.LocalName == "data").Value is { Length: > 0 } encoded
                ? Convert.FromBase64String(encoded)
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public void Resolve(OpenXmlPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        foreach (var part in package.GetAllParts().Where(Carries).ToList())
        {
            if (part.RootElement is not { } root) continue;
            var blips = root.Descendants<A.Blip>().Where(blip => Extension(blip) is not null).ToList();
            if (blips.Count == 0) continue;
            var added = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var blip in blips)
            {
                var data = Data(blip);
                var extension = Extension(blip)!;
                var list = (A.BlipExtensionList)extension.Parent!;
                extension.Remove();
                if (!list.HasChildren) list.Remove();
                if (data is null)
                {
                    blip.Embed = null;
                    continue;
                }
                var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data));
                if (!added.TryGetValue(key, out var id))
                {
                    var image = AddImage(part, data);
                    using (var stream = new MemoryStream(data)) image.FeedData(stream);
                    added[key] = id = part.GetIdOfPart(image);
                }
                blip.Embed = id;
            }
            root.Save();
        }
    }

    private A.BlipExtension? Extension(A.Blip blip) =>
        blip.GetFirstChild<A.BlipExtensionList>()?.Elements<A.BlipExtension>().FirstOrDefault(extension => extension.Uri?.Value == Uri);

    private bool Carries(OpenXmlPart part) => part is SlidePart or SlideLayoutPart or SlideMasterPart or NotesSlidePart or DrawingsPart
        or MainDocumentPart or HeaderPart or FooterPart;

    private ImagePart AddImage(OpenXmlPart part, byte[] data)
    {
        var type = _header.MediaType(data) switch
        {
            "image/jpeg" => ImagePartType.Jpeg,
            "image/gif" => ImagePartType.Gif,
            "image/bmp" => ImagePartType.Bmp,
            "image/tiff" => ImagePartType.Tiff,
            _ => ImagePartType.Png,
        };
        return part switch
        {
            SlidePart slide => slide.AddImagePart(type),
            SlideLayoutPart layout => layout.AddImagePart(type),
            SlideMasterPart master => master.AddImagePart(type),
            NotesSlidePart notes => notes.AddImagePart(type),
            DrawingsPart drawings => drawings.AddImagePart(type),
            MainDocumentPart main => main.AddImagePart(type),
            HeaderPart header => header.AddImagePart(type),
            FooterPart footer => footer.AddImagePart(type),
            _ => throw new InvalidOperationException($"Part {part.Uri} cannot carry images."),
        };
    }
}

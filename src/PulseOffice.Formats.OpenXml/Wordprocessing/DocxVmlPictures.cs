using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Assets;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxVmlPictures(ImageHeader header)
{
    public RasterImageObject? Read(OpenXmlElement element, DocxReadContext context)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(context);
        if (element.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().FirstOrDefault() is not { } data
            || data.RelationshipId?.Value is not { Length: > 0 } relationship
            || !context.Part.TryGetPartById(relationship, out var found) || found is not ImagePart part)
            return null;

        using var stream = part.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var pixels = header.Size(bytes);
        return new RasterImageObject
        {
            Id = context.Document.NextId("obj"),
            Data = bytes,
            PixelWidth = pixels?.Width ?? 0,
            PixelHeight = pixels?.Height ?? 0,
            MediaType = part.ContentType,
        };
    }

    public bool Holds(OpenXmlElement element) => element.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().Any();
}

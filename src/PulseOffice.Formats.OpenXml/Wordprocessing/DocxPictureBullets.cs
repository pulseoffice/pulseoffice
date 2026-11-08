using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Styles;
using A = DocumentFormat.OpenXml.Drawing;
using V = DocumentFormat.OpenXml.Vml;
using W = DocumentFormat.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxPictureBullets(OoxmlImageTypes types, VmlStyle vmlStyles)
{
    private const double DefaultSize = 9;

    public int Add(W.Numbering numbering, NumberingDefinitionsPart part, ListBulletPicture picture)
    {
        var id = numbering.Elements<W.NumberingPictureBullet>().Count();
        var image = part.AddImagePart(types.PartOf(picture.MediaType));
        using (var stream = image.GetStream(FileMode.Create)) stream.Write(picture.Data.Span);
        var style = string.Create(CultureInfo.InvariantCulture, $"width:{picture.Width:0.###}pt;height:{picture.Height:0.###}pt");
        var shape = new V.Shape(new V.ImageData { RelationshipId = part.GetIdOfPart(image), Title = string.Empty })
        {
            Id = "_x0000_i" + (1025 + id).ToString(CultureInfo.InvariantCulture),
            Style = style,
            Bullet = true,
        };
        var bullet = new W.NumberingPictureBullet(new W.PictureBulletBase(shape)) { NumberingPictureBulletId = id };
        numbering.InsertAt(bullet, id);
        return id;
    }

    public ListBulletPicture? Read(W.Numbering numbering, int id)
    {
        if (numbering.NumberingDefinitionsPart is not { } part) return null;
        var bullet = numbering.Elements<W.NumberingPictureBullet>().FirstOrDefault(item => item.NumberingPictureBulletId?.Value == id);
        if (bullet is null) return null;
        var relationship = bullet.Descendants<V.ImageData>().Select(data => data.RelationshipId?.Value).FirstOrDefault(value => value is { Length: > 0 })
            ?? bullet.Descendants<A.Blip>().Select(blip => blip.Embed?.Value).FirstOrDefault(value => value is { Length: > 0 });
        if (relationship is null || !part.TryGetPartById(relationship, out var found) || found is not ImagePart image) return null;
        using var stream = image.GetStream(FileMode.Open, FileAccess.Read);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        var (width, height) = Size(bullet);
        return new ListBulletPicture(copy.ToArray(), image.ContentType, width, height) { ScalesWithFont = true };
    }

    private (double Width, double Height) Size(W.NumberingPictureBullet bullet)
    {
        if (bullet.Descendants<V.Shape>().FirstOrDefault()?.Style?.Value is { Length: > 0 } style)
        {
            var parsed = vmlStyles.Parse(style);
            return (Points(parsed, "width"), Points(parsed, "height"));
        }
        if (bullet.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().FirstOrDefault() is { } extent)
            return ((extent.Cx?.Value ?? 0) / 12700.0, (extent.Cy?.Value ?? 0) / 12700.0);
        return (DefaultSize, DefaultSize);
    }

    private double Points(IReadOnlyDictionary<string, string> style, string key) =>
        vmlStyles.Length(style, key) is { Value: > 0 } length ? length.ToPoints() : DefaultSize;
}

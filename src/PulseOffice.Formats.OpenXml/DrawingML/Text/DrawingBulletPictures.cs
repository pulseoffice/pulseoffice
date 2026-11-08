using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Assets;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal interface IDrawingBulletPictures
{
    ListBulletPicture? Read(OpenXmlElement properties);

    IEnumerable<OpenXmlElement> Write(BulletSymbol symbol, ListBulletPicture picture);
}

internal sealed class DrawingBulletPictures(OoxmlPendingImages pending, ImageHeader header) : IDrawingBulletPictures
{
    private const long LargestPicture = 8L * 1024 * 1024;

    private const double NominalHeight = 12;

    private const int LargestPoints = 400000;

    private const double SmallestProportion = 0.1;

    private const double LargestProportion = 10;

    public DrawingBulletPictures() : this(new OoxmlPendingImages(), new ImageHeader())
    {
    }

    public ListBulletPicture? Read(OpenXmlElement properties)
    {
        if (properties.GetFirstChild<D.PictureBullet>()?.GetFirstChild<D.Blip>() is not { } blip) return null;
        if ((pending.Data(blip) ?? Embedded(blip)) is not { Length: > 0 } data) return null;
        var proportion = Proportion(data);
        var points = properties.GetFirstChild<D.BulletSizePoints>()?.Val?.Value;
        var height = points is > 0 ? Math.Min(points.Value, LargestPoints) / 100.0 : NominalHeight;
        return new ListBulletPicture(data, header.MediaType(data) ?? "image/png", height * proportion, height)
        {
            ScalesWithFont = points is not > 0,
        };
    }

    public IEnumerable<OpenXmlElement> Write(BulletSymbol symbol, ListBulletPicture picture)
    {
        if (picture.ScalesWithFont)
            yield return new D.BulletSizePercentage { Val = (int)Math.Round(Math.Clamp(symbol.RelativeSize ?? 1, 0.25, 4) * 100000) };
        else if (double.IsFinite(picture.Height) && picture.Height > 0)
            yield return new D.BulletSizePoints { Val = (int)Math.Round(Math.Clamp(picture.Height, 1, 4000) * 100) };
        yield return new D.PictureBullet(pending.Blip(picture.Data.ToArray()));
    }

    private double Proportion(byte[] data) =>
        header.Size(data) is { Width: > 0, Height: > 0 } size
            ? Math.Clamp((double)size.Width / size.Height, SmallestProportion, LargestProportion)
            : 1;

    private byte[]? Embedded(D.Blip blip)
    {
        if (blip.Embed?.Value is not { Length: > 0 } id || blip.Ancestors<OpenXmlPartRootElement>().FirstOrDefault()?.OpenXmlPart is not { } part) return null;
        if (!part.Parts.Any(pair => pair.RelationshipId == id) || part.GetPartById(id) is not ImagePart image) return null;
        using var stream = image.GetStream();
        using var copy = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (copy.Length + read > LargestPicture) return null;
            copy.Write(buffer, 0, read);
        }
        return copy.ToArray();
    }
}

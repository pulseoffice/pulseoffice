using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Sheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxSignatureLineMapper(VmlSignatureLineCodec codec, VmlStyle styles, XlsxVmlAnchor anchors, XlsxVmlDocuments documents)
{
    private readonly XNamespace _excel = "urn:schemas-microsoft-com:office:excel";

    public IEnumerable<XElement> Shapes(VmlDrawingPart vml, Sheet sheet, XlsxVmlShapes shapes)
    {
        var placed = sheet.Objects.Where(item => item.Content is SignatureLineObject).OrderBy(item => item.ZOrder).ToList();
        if (placed.Count == 0) yield break;
        yield return codec.PictureType();
        foreach (var item in placed)
        {
            var line = (SignatureLineObject)item.Content;
            var vector = vml.AddImagePart(ImagePartType.Svg);
            Fill(vector, codec.UnsignedImage(line, item.Size));
            var image = vector;
            if (codec.UnsignedRaster(line, item.Size) is { } raster)
            {
                image = vml.AddImagePart(ImagePartType.Png);
                Fill(image, raster);
            }
            var left = sheet.Layout.ColumnOffset(item.Anchor.Column) + item.OffsetX;
            var top = sheet.Layout.RowOffset(item.Anchor.Row) + item.OffsetY;
            var style = string.Create(CultureInfo.InvariantCulture,
                $"position:absolute;margin-left:{styles.Points(left)};margin-top:{styles.Points(top)};width:{styles.Points(item.Size.Width)};height:{styles.Points(item.Size.Height)};z-index:{Math.Max(0, item.ZOrder) + 1}");
            var shape = codec.Shape(line, shapes.Next(), style, new XAttribute(codec.Office + "relid", vml.GetIdOfPart(image)));
            shape.Add(new XElement(shapes.Excel + "ClientData", new XAttribute("ObjectType", "Pict"),
                new XElement(shapes.Excel + "SizeWithCells"),
                new XElement(shapes.Excel + "Anchor", anchors.Write(sheet.Layout, item)),
                new XElement(shapes.Excel + "CF", "Pict"),
                new XElement(shapes.Excel + "AutoPict")));
            yield return shape;
        }
    }

    private void Fill(ImagePart part, byte[] bytes)
    {
        using var stream = part.GetStream(FileMode.Create);
        stream.Write(bytes, 0, bytes.Length);
    }

    public void Read(WorksheetPart part, Sheet sheet)
    {
        foreach (var document in documents.Load(part))
            foreach (var shape in document.Descendants(codec.Vml + "shape").Where(codec.Holds))
            {
                var id = "signature" + (sheet.Objects.Count + 1).ToString(CultureInfo.InvariantCulture);
                if (codec.Read(shape, id) is not { } line) continue;
                var box = Styled(shape) ?? anchors.Read(sheet.Layout, (string?)shape.Element(_excel + "ClientData")?.Element(_excel + "Anchor"));
                if (box is not { } found) continue;
                var seed = new PlacedObject { Id = id, Content = line with { NaturalSize = new DocumentSize(found.Width, found.Height) }, Anchor = new CellAddress(0, 0), ZOrder = sheet.Objects.Count };
                sheet.Objects.Add(anchors.Place(sheet.Layout, seed, found));
            }
    }

    private PlacementBox? Styled(XElement shape)
    {
        var style = styles.Parse((string?)shape.Attribute("style"));
        if (!style.TryGetValue("position", out var position) || position != "absolute") return null;
        if (styles.Length(style, "margin-left") is not { } left || styles.Length(style, "margin-top") is not { } top
            || styles.Length(style, "width") is not { } width || styles.Length(style, "height") is not { } height) return null;
        return new PlacementBox(left, top, width, height);
    }
}

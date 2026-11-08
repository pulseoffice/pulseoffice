using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxGroupPictures(DrawingsPart drawings, XlsxImageMapper images) : IGroupPictureBlips
{
    public A.Blip? Blip(RasterImageObject raster) =>
        raster.Data.IsEmpty && raster.LinkPath is not { Length: > 0 } ? null : images.RasterBlip(drawings, raster);

    public EmbeddedObject? Read(Xdr.Picture picture, string name) => images.PictureContent(drawings, picture, name);
}

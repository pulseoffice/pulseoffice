using PulseOffice.Documents.Objects;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Controls.Objects;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioImages
{
    bool Insert(FolioEditor editor, byte[] data, string mediaType, string name);

    AnchoredObject? Build(TextDocument document, string paragraphId, byte[] data, string mediaType, string name);
}

public sealed class FolioImages(IBitmapPixels pixels, IImageExtents extents) : IFolioImages
{
    private readonly ImageBounds _bounds = new(new DocumentSize(Twip.FromCentimeters(16), Twip.FromCentimeters(24)), Twip.FromCentimeters(1));

    public bool Insert(FolioEditor editor, byte[] data, string mediaType, string name)
    {
        if (editor.ReadOnly() || data.Length == 0) return false;
        if (Build(editor.Document, editor.Caret.ParagraphId, data, mediaType, name) is not { } built) return false;
        return editor.Insert((documents, at) => documents.InsertObject(editor.Document, editor.History,
            new AnchoredObject(built.Id, new AsCharacterAnchor(at.ParagraphId), built.Content, built.Size), at), "insert-image");
    }

    public AnchoredObject? Build(TextDocument document, string paragraphId, byte[] data, string mediaType, string name)
    {
        if (data.Length == 0) return null;
        using var decoded = pixels.Decode(data);
        if (decoded is null) return null;
        var (pixelWidth, pixelHeight) = (decoded.PixelSize.Width, decoded.PixelSize.Height);
        var size = extents.Natural(pixelWidth, pixelHeight, _bounds);
        var raster = new RasterImageObject
        {
            Id = document.NextId("img"),
            Data = data,
            MediaType = mediaType,
            PixelWidth = pixelWidth,
            PixelHeight = pixelHeight,
            Name = name,
            NaturalSize = size,
        };
        return new AnchoredObject(document.NextId("o"), new AsCharacterAnchor(paragraphId), new EmbeddedContent(raster), size);
    }
}

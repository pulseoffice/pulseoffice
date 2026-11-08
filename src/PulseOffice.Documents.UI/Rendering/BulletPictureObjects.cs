using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.UI.Rendering;

public interface IBulletPictureObjects
{
    EmbeddedObject For(ListBulletPicture picture);
}

public sealed class BulletPictureObjects : IBulletPictureObjects
{
    private readonly ConditionalWeakTable<byte[], EmbeddedObject> _objects = new();

    public EmbeddedObject For(ListBulletPicture picture)
    {
        if (!MemoryMarshal.TryGetArray(picture.Data, out var segment) || segment.Array is not { } array || segment.Offset != 0 || segment.Count != array.Length)
            return Create(picture);
        return _objects.GetValue(array, _ => Create(picture));
    }

    private EmbeddedObject Create(ListBulletPicture picture) =>
        new RasterImageObject { Id = "bullet-picture", Data = picture.Data, MediaType = picture.MediaType };
}

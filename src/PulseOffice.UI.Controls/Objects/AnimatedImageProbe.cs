using System.Runtime.CompilerServices;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Assets.Gif;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.UI.Controls.Objects;

public interface IAnimatedImageProbe
{
    bool IsAnimated(RasterImageObject raster);

    bool AnyAnimated(IEnumerable<EmbeddedObject> contents);
}

public sealed class AnimatedImageProbe(IGifCodec gifs) : IAnimatedImageProbe
{
    private readonly ConditionalWeakTable<object, StrongBox<bool>> _known = new();
    private readonly Lock _gate = new();

    public AnimatedImageProbe() : this(new GifCodec()) { }

    public bool IsAnimated(RasterImageObject raster)
    {
        if (raster.MediaType != "image/gif") return false;

        var identity = RasterIdentity.Of(raster);

        lock (_gate)
        {
            if (_known.TryGetValue(identity, out var cached)) return cached.Value;
        }

        var animated = gifs.Decode(raster.Data.Span) is { Frames.Count: > 1 };

        lock (_gate) _known.AddOrUpdate(identity, new StrongBox<bool>(animated));
        return animated;
    }

    public bool AnyAnimated(IEnumerable<EmbeddedObject> contents) =>
        contents.Any(content => content switch
        {
            RasterImageObject raster => IsAnimated(raster),
            ShapeGroupObject group => AnyAnimated(group.Children.Select(child => child.Content)),
            _ => false,
        });
}

public static class RasterIdentity
{
    public static object Of(RasterImageObject raster) =>
        System.Runtime.InteropServices.MemoryMarshal.TryGetArray(raster.Data, out var segment) && segment.Array is { } array
            ? array
            : raster;
}

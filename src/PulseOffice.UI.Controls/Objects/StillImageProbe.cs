using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;

namespace PulseOffice.UI.Controls.Objects;

public sealed class StillImageProbe : IAnimatedImageProbe
{
    public bool IsAnimated(RasterImageObject raster) => false;

    public bool AnyAnimated(IEnumerable<EmbeddedObject> contents) => false;
}

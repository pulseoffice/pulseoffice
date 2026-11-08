using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Presentations;

public interface ISlideBackgrounds
{
    ShapeFill Current(Slide slide);

    SlideBackgroundChoice Choose(ShapeFill fill, bool hideGraphics);
}

public sealed class SlideBackgrounds : ISlideBackgrounds
{
    public ShapeFill Current(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        if (slide.BackgroundImage is { } image)
            return ShapeFill.Default with
            {
                Kind = ShapeFillKind.Bitmap,
                Bitmap = new ShapeBitmap
                {
                    Data = image.Data.ToArray(),
                    MediaType = image.MediaType,
                    Tiled = slide.BackgroundLayout.Mode == BackgroundImageMode.Tile,
                    TileWidth = slide.BackgroundLayout.Width,
                    TileHeight = slide.BackgroundLayout.Height,
                },
            };
        return slide.BackgroundFill ?? ShapeFill.Default with { Kind = ShapeFillKind.None };
    }

    public SlideBackgroundChoice Choose(ShapeFill fill, bool hideGraphics)
    {
        ArgumentNullException.ThrowIfNull(fill);
        if (fill.Kind is ShapeFillKind.Bitmap && fill.Bitmap is { } bitmap)
            return new SlideBackgroundChoice(
                null,
                new RasterImageObject { Id = Guid.NewGuid().ToString("N"), Data = bitmap.Data, MediaType = bitmap.MediaType },
                bitmap.Tiled ? new BackgroundImageLayout(BackgroundImageMode.Tile, bitmap.TileWidth, bitmap.TileHeight) : BackgroundImageLayout.Stretched,
                hideGraphics);
        return new SlideBackgroundChoice(fill.Kind is ShapeFillKind.None ? null : fill, null, BackgroundImageLayout.Stretched, hideGraphics);
    }
}

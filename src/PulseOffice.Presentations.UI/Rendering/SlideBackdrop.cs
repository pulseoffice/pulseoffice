using Avalonia.Media;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Presentations.UI.Rendering;

public interface ISlideBackdrop
{
    Color Background(Slide slide, SlideMaster master, Theme theme);

    ShapeFill? Fill(Slide slide, SlideMaster master);

    ShapeSurroundings For(Slide slide, SlideMaster master, Theme theme);
}

public sealed class SlideBackdrop : ISlideBackdrop
{
    public Color Background(Slide slide, SlideMaster master, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(master);
        return slide.Background is { } argb ? Color.FromUInt32(argb)
            : slide.ShowMasterBackground && slide.BackgroundImage is null ? Color.FromUInt32(master.BackgroundOf(theme, slide)) : Colors.White;
    }

    public ShapeFill? Fill(Slide slide, SlideMaster master)
    {
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(master);
        return slide.BackgroundImage is not null ? null
            : slide.BackgroundFill is { } own ? own
            : slide.Background is null && slide.ShowMasterBackground && master.BackgroundImage is null ? master.BackgroundFill
            : null;
    }

    public ShapeSurroundings For(Slide slide, SlideMaster master, Theme theme) =>
        Fill(slide, master) is { IsVisible: true } painted && painted.Kind is not ShapeFillKind.Bitmap
            && Color.TryParse(painted.Kind is ShapeFillKind.Gradient ? painted.Gradient.StartHex : painted.ColorHex, out var colour)
            ? new ShapeSurroundings(colour)
            : new ShapeSurroundings(Background(slide, master, theme));
}

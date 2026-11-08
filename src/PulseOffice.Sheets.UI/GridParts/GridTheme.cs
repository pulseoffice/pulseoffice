using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.GridParts;

internal interface IGridTheme
{
    IBrush Brush(string key, IBrush fallback);

    IBrush Accent { get; }

    ShapeSurroundings Backdrop();

    FormControlPalette FormPalette();

    PulseOffice.UI.Controls.Objects.PackagedPalette PackagedPalette();
}

internal sealed class ResourceGridTheme(Control owner) : IGridTheme
{
    public IBrush Brush(string key, IBrush fallback) => owner.FindResource(key) as IBrush ?? fallback;

    public IBrush Accent => owner.FindResource("PulseAccent") as IBrush ?? Brushes.SteelBlue;

    public ShapeSurroundings Backdrop() =>
        Brush(Palette.Keys.Surface, Brushes.White) is ISolidColorBrush surface ? new ShapeSurroundings(surface.Color) : new ShapeSurroundings();

    public FormControlPalette FormPalette() => new(
        Brush(Palette.Keys.SurfaceRaised, Brushes.WhiteSmoke),
        Brush(Palette.Keys.Surface, Brushes.White),
        Brush(Palette.Keys.BorderStrong, Brushes.Gray),
        Brush(Palette.Keys.TextPrimary, Brushes.Black),
        Brush(Palette.Keys.TextSecondary, Brushes.DimGray),
        Accent,
        Brush(Palette.Keys.TextDisabled, Brushes.Silver));

    public PulseOffice.UI.Controls.Objects.PackagedPalette PackagedPalette() => new(
        Brush(Palette.Keys.SurfaceOverlay, Brushes.White),
        Brush(Palette.Keys.Border, Brushes.Gray),
        Brush(Palette.Keys.TextPrimary, Brushes.Black),
        Accent);
}

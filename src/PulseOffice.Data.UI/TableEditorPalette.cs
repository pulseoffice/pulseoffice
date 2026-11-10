using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.UI.Theming;

namespace PulseOffice.Data.UI;

public sealed record TableEditorPalette(
    IBrush Surface,
    IBrush Band,
    IBrush GridLine,
    IBrush Chrome,
    IBrush HeaderField,
    IBrush Selection,
    IBrush SelectedChrome,
    IBrush Accent,
    IBrush Text,
    IBrush MutedText);

public interface ITableEditorPaletteSource
{
    TableEditorPalette Resolve(Control host);
}

public sealed class TableEditorPaletteSource : ITableEditorPaletteSource
{
    private static readonly Color FallbackAccent = Color.FromRgb(0x25, 0x63, 0xEB);

    public TableEditorPalette Resolve(Control host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var surface = ColorOf(host, Palette.Keys.Surface, Colors.White);
        var accent = ColorOf(host, "PulseAccent", FallbackAccent);
        var chrome = ColorOf(host, Palette.Keys.SurfaceRaised, Colors.WhiteSmoke);

        return new TableEditorPalette(
            Surface: BrushOf(host, Palette.Keys.Surface, Brushes.White),
            Band: new SolidColorBrush(ColorOf(host, Palette.Keys.SurfaceSunken, Colors.WhiteSmoke)),
            GridLine: BrushOf(host, Palette.Keys.Border, Brushes.Gainsboro),
            Chrome: new SolidColorBrush(chrome),
            HeaderField: new SolidColorBrush(Blend(surface, accent, 0.12)),
            Selection: new SolidColorBrush(Blend(surface, accent, 0.22)),
            SelectedChrome: new SolidColorBrush(Blend(chrome, accent, 0.28)),
            Accent: new SolidColorBrush(accent),
            Text: BrushOf(host, Palette.Keys.TextPrimary, Brushes.Black),
            MutedText: BrushOf(host, Palette.Keys.TextSecondary, Brushes.Gray));
    }

    private static IBrush BrushOf(Control host, string key, IBrush fallback) =>
        host.FindResource(key) as IBrush ?? fallback;

    private static Color ColorOf(Control host, string key, Color fallback) => host.FindResource(key) switch
    {
        ISolidColorBrush brush => Opaque(brush.Color, brush.Opacity),
        Color color => color,
        _ => fallback,
    };

    private static Color Opaque(Color color, double opacity)
    {
        var alpha = color.A / 255.0 * opacity;
        var solid = Color.FromRgb(color.R, color.G, color.B);
        return alpha >= 1 ? solid : Blend(Colors.White, solid, alpha);
    }

    private static Color Blend(Color under, Color over, double amount) => Color.FromRgb(
        (byte)Math.Round(under.R + (over.R - under.R) * amount),
        (byte)Math.Round(under.G + (over.G - under.G) * amount),
        (byte)Math.Round(under.B + (over.B - under.B) * amount));
}

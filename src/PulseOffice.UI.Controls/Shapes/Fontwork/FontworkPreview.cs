using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Drawing.Fontwork;

namespace PulseOffice.UI.Controls.Shapes.Fontwork;

public sealed class FontworkPreview(IFontworkRenderer renderer, Thickness inset) : Control
{
    private FontworkObject? _fontwork;

    public FontworkObject? Fontwork => _fontwork;

    public void Show(FontworkObject fontwork)
    {
        _fontwork = fontwork ?? throw new ArgumentNullException(nameof(fontwork));
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var area = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.White, area);
        if (_fontwork is null) return;

        var art = area.Deflate(inset);
        if (art.Width <= 0 || art.Height <= 0) return;
        using (context.PushClip(area))
            renderer.Render(context, _fontwork, art);
    }
}

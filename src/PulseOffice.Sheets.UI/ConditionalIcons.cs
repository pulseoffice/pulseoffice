using Avalonia;
using Avalonia.Media;
using PulseOffice.Drawing.ConditionalIcons;
using PulseOffice.Sheets.Formatting;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Sheets.UI;

internal sealed class ConditionalIcons(IConditionalIconCatalog catalog, IconPainter painter)
{
    public ConditionalIcons() : this(new ConditionalIconCatalog(), new IconPainter()) { }

    public double Reserve(Rect bounds) => Math.Min(bounds.Height, 18);

    public void Draw(DrawingContext context, ConditionalAppearance appearance, Rect bounds)
    {
        if (appearance.IconIndex is not { } index || appearance.IconSetName is not { } name) return;

        var size = Math.Max(6, Reserve(bounds) - 5);
        var box = new Rect(bounds.X + 3, bounds.Y + (bounds.Height - size) / 2, size, size);
        painter.Paint(context, catalog.Icon(name, index), box);
    }
}

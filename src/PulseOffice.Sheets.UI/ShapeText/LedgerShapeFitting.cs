using PulseOffice.Foundation.Units;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Sheets.UI.ShapeText;

public interface ILedgerShapeFitting
{
    PlacedObject Fit(PlacedObject placed);
}

public sealed class LedgerShapeFitting(IShapeTextFit fit) : ILedgerShapeFitting
{
    public LedgerShapeFitting() : this(new ShapeTextFit())
    {
    }

    public PlacedObject Fit(PlacedObject placed)
    {
        ArgumentNullException.ThrowIfNull(placed);
        if (placed.Content is not PulseOffice.Drawing.Shapes.ShapeObject shape || fit.Height(shape, placed.Size) is not { } height) return placed;
        return height == placed.Size.Height ? placed : placed with { Size = new DocumentSize(placed.Size.Width, height) };
    }
}

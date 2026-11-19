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
        if (placed.Content is not PulseOffice.Drawing.Shapes.ShapeObject shape || fit.Size(shape, placed.Size) is not { } size) return placed;
        return size == placed.Size ? placed : placed with { Size = size };
    }
}

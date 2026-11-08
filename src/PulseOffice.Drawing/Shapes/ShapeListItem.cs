using PulseOffice.Text.Rich;

namespace PulseOffice.Drawing.Shapes;

public sealed record ShapeListItem
{
    public int Level { get; init; }

    public bool Numbered { get; init; }

    public BulletSymbol? Symbol { get; init; }

    public ListNumberFormat? NumberFormat { get; init; }

    public double? Margin { get; init; }

    public double? Indent { get; init; }
}

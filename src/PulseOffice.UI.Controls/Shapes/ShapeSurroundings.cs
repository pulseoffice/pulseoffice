using Avalonia.Media;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.UI.Controls.Shapes;

public readonly record struct ShapeSurroundings(string Hex)
{
    public ShapeSurroundings() : this(ShapeTextInk.PaperHex) { }

    public ShapeSurroundings(Color colour) : this($"#{colour.R:X2}{colour.G:X2}{colour.B:X2}") { }

    public ShapeSurroundings(uint argb) : this(Color.FromUInt32(argb)) { }

    public string? Language { get; init; }
}

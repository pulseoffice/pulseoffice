using Avalonia.Media;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.UI;

public sealed class CellTextMeasure(ICellTextComposer composer) : ICellTextMeasure
{
    public TextExtent Measure(string text, CellFont font, double? wrapWidth, int rotationDegrees)
    {
        var formatted = composer.Compose(text, font, Brushes.Black);
        if (wrapWidth is { } width && rotationDegrees % 360 == 0) composer.Wrap(formatted, width, CellHorizontalAlignment.Left);
        return composer.Extent(formatted, rotationDegrees);
    }
}

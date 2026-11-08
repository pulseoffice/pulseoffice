using DocumentFormat.OpenXml;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal sealed class DrawingTabStops
{

    public D.TabStopList Write(IReadOnlyList<TabStop> stops) =>
        new(stops.Select(stop => new D.TabStop
        {
            Position = (int)Math.Round(stop.Position * ((double)PulseOffice.Foundation.Units.Emu.PerPoint)),
            Alignment = stop.Alignment switch
            {
                TabAlignment.Center => D.TextTabAlignmentValues.Center,
                TabAlignment.Right => D.TextTabAlignmentValues.Right,
                TabAlignment.Decimal => D.TextTabAlignmentValues.Decimal,
                _ => D.TextTabAlignmentValues.Left,
            },
        }));

    public IReadOnlyList<TabStop>? Read(OpenXmlElement properties) =>
        properties.GetFirstChild<D.TabStopList>() is { } list
            ? [.. list.Elements<D.TabStop>().Select(tab => new TabStop((tab.Position?.Value ?? 0) / ((double)PulseOffice.Foundation.Units.Emu.PerPoint))
            {
                Alignment = tab.Alignment?.Value is { } alignment
                    ? alignment == D.TextTabAlignmentValues.Center ? TabAlignment.Center
                    : alignment == D.TextTabAlignmentValues.Right ? TabAlignment.Right
                    : alignment == D.TextTabAlignmentValues.Decimal ? TabAlignment.Decimal
                    : TabAlignment.Left
                    : TabAlignment.Left,
            })]
            : null;
}

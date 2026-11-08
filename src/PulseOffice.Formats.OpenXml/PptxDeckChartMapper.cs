using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

public sealed class PptxDeckChartMapper
{
    private const string ChartUri = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    private readonly InlineChartParts _charts = new();

    public P.GraphicFrame Build(ObjectElement element, ChartDefinition chart, uint id, SlidePart slidePart)
    {
        var relationship = _charts.Write(slidePart, chart);

        var bounds = element.Bounds;
        return new P.GraphicFrame(
            new P.NonVisualGraphicFrameProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = "Chart" },
                new P.NonVisualGraphicFrameDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.Transform(
                new D.Offset { X = Emu.FromTwips(bounds.Left).Value, Y = Emu.FromTwips(bounds.Top).Value },
                new D.Extents { Cx = Emu.FromTwips(bounds.Width).Value, Cy = Emu.FromTwips(bounds.Height).Value }),
            new D.Graphic(new D.GraphicData(
                new C.ChartReference { Id = relationship }) { Uri = ChartUri }));
    }

    public ObjectElement? Read(P.GraphicFrame frame, SlidePart slidePart, string id)
    {
        var reference = frame.Graphic?.GraphicData?.GetFirstChild<C.ChartReference>()?.Id?.Value;
        if (_charts.Read(slidePart, reference, id) is not { } definition) return null;

        var offset = frame.Transform?.Offset;
        var extents = frame.Transform?.Extents;
        var bounds = new DocumentRect(
            new Emu(offset?.X?.Value ?? 0).ToTwips(),
            new Emu(offset?.Y?.Value ?? 0).ToTwips(),
            new Emu(extents?.Cx?.Value ?? 0).ToTwips(),
            new Emu(extents?.Cy?.Value ?? 0).ToTwips());

        return new ObjectElement { Id = id, Bounds = bounds, Content = definition };
    }
}

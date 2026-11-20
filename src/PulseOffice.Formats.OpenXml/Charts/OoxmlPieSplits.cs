using DocumentFormat.OpenXml;
using PulseOffice.Charts;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlPieSplits
{
    public void Write(OpenXmlCompositeElement chart, ChartPieSplit split)
    {
        if (chart is not C.OfPieChart pie) return;
        var size = pie.GetFirstChild<C.SecondPieSize>();
        if (size is null)
        {
            size = new C.SecondPieSize();
            pie.InsertBefore(size, pie.GetFirstChild<C.SeriesLines>());
        }
        size.Val = (ushort)Math.Clamp(split.SecondPlotSize, ChartPieSplit.MinimumSecondPlotSize, ChartPieSplit.MaximumSecondPlotSize);
        if (Kind(split.Kind) is not { } kind) return;
        size.InsertBeforeSelf(new C.SplitType { Val = kind });
        if (split.Kind == PieSplitKind.Custom)
            size.InsertBeforeSelf(new C.CustomSplit(split.CustomPoints.Where(point => point >= 0).Distinct().Order().Select(point => new C.SecondPiePoint { Val = (uint)point })));
        else
            size.InsertBeforeSelf(new C.SplitPosition { Val = split.Threshold });
    }

    public ChartPieSplit Read(C.PlotArea plot)
    {
        if (plot.GetFirstChild<C.OfPieChart>() is not { } pie) return ChartPieSplit.Automatic;
        var value = pie.GetFirstChild<C.SplitType>()?.Val?.Value;
        var kind = value == C.SplitValues.Position ? PieSplitKind.Position
            : value == C.SplitValues.Value ? PieSplitKind.Value
            : value == C.SplitValues.Percent ? PieSplitKind.Percent
            : value == C.SplitValues.Custom ? PieSplitKind.Custom
            : PieSplitKind.Automatic;
        var size = pie.GetFirstChild<C.SecondPieSize>()?.Val?.Value is { } read ? (int)read : ChartPieSplit.DefaultSecondPlotSize;
        return new ChartPieSplit
        {
            Kind = kind,
            Threshold = kind is PieSplitKind.Position or PieSplitKind.Value or PieSplitKind.Percent ? pie.GetFirstChild<C.SplitPosition>()?.Val?.Value ?? 0 : 0,
            CustomPoints = kind == PieSplitKind.Custom
                ? [.. pie.GetFirstChild<C.CustomSplit>()?.Elements<C.SecondPiePoint>().Select(point => (int)(point.Val?.Value ?? 0)) ?? []]
                : [],
            SecondPlotSize = Math.Clamp(size, ChartPieSplit.MinimumSecondPlotSize, ChartPieSplit.MaximumSecondPlotSize),
        };
    }

    private C.SplitValues? Kind(PieSplitKind kind) => kind switch
    {
        PieSplitKind.Position => C.SplitValues.Position,
        PieSplitKind.Value => C.SplitValues.Value,
        PieSplitKind.Percent => C.SplitValues.Percent,
        PieSplitKind.Custom => C.SplitValues.Custom,
        _ => null,
    };
}

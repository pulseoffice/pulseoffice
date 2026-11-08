using DocumentFormat.OpenXml;
using PulseOffice.Charts;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml;

internal readonly record struct ChartGrouping(bool Stacked, bool Percent)
{
    public static ChartGrouping Of(ChartType type) => new(
        type is ChartType.StackedColumn or ChartType.StackedBar or ChartType.StackedLine or ChartType.StackedArea,
        type is ChartType.PercentStackedColumn or ChartType.PercentStackedBar or ChartType.PercentStackedLine or ChartType.PercentStackedArea);

    public C.GroupingValues Standard => Percent ? C.GroupingValues.PercentStacked : Stacked ? C.GroupingValues.Stacked : C.GroupingValues.Standard;

    public C.BarGroupingValues Bars => Percent ? C.BarGroupingValues.PercentStacked : Stacked ? C.BarGroupingValues.Stacked : C.BarGroupingValues.Clustered;
}

internal sealed record ChartFamilyRead(ChartType Type, IReadOnlyList<OpenXmlCompositeElement> Series)
{
    public bool ThreeDimensional { get; init; }
}

internal abstract class ChartFamily
{
    public abstract bool Writes(ChartType type);

    public abstract OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional);

    public abstract OpenXmlCompositeElement Series();

    public virtual bool HasAxes => true;

    public virtual bool DeclaresType => false;

    public virtual bool ValueCategoryAxis => false;

    public virtual bool ColoursPoints => false;

    public virtual bool DrawsLines(ChartType type) => false;

    public virtual bool WritesThreeDimensional(ChartType type) => false;

    public virtual C.View3D View(ChartType type) => new(new C.RotateX { Val = 15 }, new C.RotateY { Val = 20 }, new C.RightAngleAxes { Val = true });

    public virtual OpenXmlElement Categories(OpenXmlElement categories) => categories;

    public virtual IReadOnlyList<IReadOnlyList<int>> Bundles(IReadOnlyList<int> members) => [.. members.Select(member => (IReadOnlyList<int>)[member])];

    public virtual int Lead(IReadOnlyList<int> bundle) => bundle[0];

    public virtual IEnumerable<OpenXmlElement> Data(ISeriesSource source, ChartData data, IReadOnlyList<int> bundle) =>
        [Categories(source.Categories(data.Categories)), Values(source.Values(bundle[0], data.Series[bundle[0]].Values))];

    public virtual OpenXmlElement Values(OpenXmlElement values) => values;

    public virtual void Finish(OpenXmlCompositeElement series, ChartDefinition definition) { }

    public virtual void Close(OpenXmlCompositeElement chart, ChartType type, bool threeDimensional) { }

    public abstract ChartFamilyRead? Read(C.PlotArea plot);

    protected void InsertAfterProperties(OpenXmlCompositeElement series, OpenXmlElement element)
    {
        if (series.GetFirstChild<C.ChartShapeProperties>() is { } properties) properties.InsertAfterSelf(element);
        else if (series.GetFirstChild<C.SeriesText>() is { } text) text.InsertAfterSelf(element);
        else series.AppendChild(element);
    }
}

internal sealed class BarFamily : ChartFamily
{
    public override bool Writes(ChartType type) => true;

    public override bool WritesThreeDimensional(ChartType type) => true;

    public override OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional)
    {
        var direction = new C.BarDirection { Val = type is ChartType.Bar or ChartType.StackedBar or ChartType.PercentStackedBar ? C.BarDirectionValues.Bar : C.BarDirectionValues.Column };
        var grouping = new C.BarGrouping { Val = ChartGrouping.Of(type).Bars };
        var vary = new C.VaryColors { Val = false };
        return threeDimensional ? new C.Bar3DChart(direction, grouping, vary) : new C.BarChart(direction, grouping, vary);
    }

    public override OpenXmlCompositeElement Series() => new C.BarChartSeries();

    public override ChartFamilyRead? Read(C.PlotArea plot)
    {
        OpenXmlCompositeElement? bar = plot.GetFirstChild<C.BarChart>();
        var threeDimensional = false;
        if (bar is null && plot.GetFirstChild<C.Bar3DChart>() is { } solid)
        {
            bar = solid;
            threeDimensional = true;
        }
        if (bar is null) return null;
        return new ChartFamilyRead(TypeOf(bar), [.. bar.Elements<C.BarChartSeries>()]) { ThreeDimensional = threeDimensional };
    }

    private ChartType TypeOf(OpenXmlCompositeElement bar)
    {
        var horizontal = bar.GetFirstChild<C.BarDirection>()?.Val?.Value == C.BarDirectionValues.Bar;
        var grouping = bar.GetFirstChild<C.BarGrouping>()?.Val?.Value;
        var stacked = grouping == C.BarGroupingValues.Stacked;
        var percent = grouping == C.BarGroupingValues.PercentStacked;

        return horizontal
            ? percent ? ChartType.PercentStackedBar : stacked ? ChartType.StackedBar : ChartType.Bar
            : percent ? ChartType.PercentStackedColumn : stacked ? ChartType.StackedColumn : ChartType.Column;
    }
}

internal sealed class LineFamily : ChartFamily
{
    public override bool Writes(ChartType type) =>
        type is ChartType.Line or ChartType.SmoothLine or ChartType.SteppedLine or ChartType.StackedLine or ChartType.PercentStackedLine;

    public override bool DrawsLines(ChartType type) => true;

    public override OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional) =>
        new C.LineChart(new C.Grouping { Val = ChartGrouping.Of(type).Standard }, new C.VaryColors { Val = false });

    public override OpenXmlCompositeElement Series() => new C.LineChartSeries();

    public override void Finish(OpenXmlCompositeElement series, ChartDefinition definition) =>
        series.AppendChild(new C.Smooth { Val = definition.Type == ChartType.SmoothLine });

    public override ChartFamilyRead? Read(C.PlotArea plot)
    {
        OpenXmlCompositeElement? line = plot.GetFirstChild<C.LineChart>();
        var threeDimensional = false;
        if (line is null && plot.GetFirstChild<C.Line3DChart>() is { } solid)
        {
            line = solid;
            threeDimensional = true;
        }
        if (line is null) return null;

        var grouping = line.GetFirstChild<C.Grouping>()?.Val?.Value;
        var smooth = line.Elements<C.LineChartSeries>().Any(s => s.GetFirstChild<C.Smooth>()?.Val?.Value == true);
        var type = grouping == C.GroupingValues.Stacked ? ChartType.StackedLine
            : grouping == C.GroupingValues.PercentStacked ? ChartType.PercentStackedLine
            : smooth ? ChartType.SmoothLine
            : ChartType.Line;
        return new ChartFamilyRead(type, [.. line.Elements<C.LineChartSeries>()]) { ThreeDimensional = threeDimensional };
    }
}

internal sealed class AreaFamily : ChartFamily
{
    public override bool Writes(ChartType type) =>
        type is ChartType.Area or ChartType.StackedArea or ChartType.PercentStackedArea;

    public override bool WritesThreeDimensional(ChartType type) => true;

    public override OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional)
    {
        var grouping = new C.Grouping { Val = ChartGrouping.Of(type).Standard };
        var vary = new C.VaryColors { Val = false };
        return threeDimensional ? new C.Area3DChart(grouping, vary) : new C.AreaChart(grouping, vary);
    }

    public override OpenXmlCompositeElement Series() => new C.AreaChartSeries();

    public override ChartFamilyRead? Read(C.PlotArea plot)
    {
        OpenXmlCompositeElement? area = plot.GetFirstChild<C.AreaChart>();
        var threeDimensional = false;
        if (area is null && plot.GetFirstChild<C.Area3DChart>() is { } solid)
        {
            area = solid;
            threeDimensional = true;
        }
        if (area is null) return null;

        var grouping = area.GetFirstChild<C.Grouping>()?.Val?.Value;
        var type = grouping == C.GroupingValues.Stacked ? ChartType.StackedArea
            : grouping == C.GroupingValues.PercentStacked ? ChartType.PercentStackedArea
            : ChartType.Area;
        return new ChartFamilyRead(type, [.. area.Elements<C.AreaChartSeries>()]) { ThreeDimensional = threeDimensional };
    }
}

internal sealed class PieFamily : ChartFamily
{
    private const uint Explosion = 25;

    public override bool Writes(ChartType type) => type is ChartType.Pie or ChartType.ExplodedPie;

    public override bool WritesThreeDimensional(ChartType type) => true;

    public override C.View3D View(ChartType type) => new(new C.RotateX { Val = 30 }, new C.RotateY { Val = 0 }, new C.RightAngleAxes { Val = false });

    public override OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional) =>
        threeDimensional ? new C.Pie3DChart(new C.VaryColors { Val = true }) : new C.PieChart(new C.VaryColors { Val = true });

    public override void Close(OpenXmlCompositeElement chart, ChartType type, bool threeDimensional)
    {
        if (!threeDimensional) chart.AppendChild(new C.FirstSliceAngle { Val = 0 });
    }

    public override OpenXmlCompositeElement Series() => new C.PieChartSeries();

    public override bool ColoursPoints => true;

    public override bool HasAxes => false;

    public override void Finish(OpenXmlCompositeElement series, ChartDefinition definition)
    {
        if (definition.Type == ChartType.ExplodedPie) InsertAfterProperties(series, new C.Explosion { Val = Explosion });
    }

    public override ChartFamilyRead? Read(C.PlotArea plot)
    {
        OpenXmlCompositeElement? pie = plot.GetFirstChild<C.PieChart>();
        var threeDimensional = false;
        if (pie is null && plot.GetFirstChild<C.Pie3DChart>() is { } solid)
        {
            pie = solid;
            threeDimensional = true;
        }
        if (pie is null) return null;
        var series = pie.Elements<C.PieChartSeries>().ToList();
        var exploded = series.Count > 0 && series.All(element => element.GetFirstChild<C.Explosion>()?.Val?.Value is > 0);
        return new ChartFamilyRead(exploded ? ChartType.ExplodedPie : ChartType.Pie, series) { ThreeDimensional = threeDimensional };
    }
}

internal sealed class OfPieFamily : ChartFamily
{
    public override bool Writes(ChartType type) => type is ChartType.PieOfPie or ChartType.BarOfPie;

    public override OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional) =>
        new C.OfPieChart(new C.OfPieType { Val = type == ChartType.BarOfPie ? C.OfPieValues.Bar : C.OfPieValues.Pie }, new C.VaryColors { Val = true });

    public override void Close(OpenXmlCompositeElement chart, ChartType type, bool threeDimensional)
    {
        chart.AppendChild(new C.GapWidth { Val = 100 });
        chart.AppendChild(new C.SecondPieSize { Val = 75 });
        chart.AppendChild(new C.SeriesLines());
    }

    public override OpenXmlCompositeElement Series() => new C.PieChartSeries();

    public override bool ColoursPoints => true;

    public override bool HasAxes => false;

    public override ChartFamilyRead? Read(C.PlotArea plot) =>
        plot.GetFirstChild<C.OfPieChart>() is { } pie
            ? new ChartFamilyRead(pie.GetFirstChild<C.OfPieType>()?.Val?.Value == C.OfPieValues.Bar ? ChartType.BarOfPie : ChartType.PieOfPie, [.. pie.Elements<C.PieChartSeries>()])
            : null;
}

internal sealed class DoughnutFamily : ChartFamily
{
    public override bool Writes(ChartType type) => type is ChartType.Doughnut;

    public override OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional) => new C.DoughnutChart(new C.VaryColors { Val = true });

    public override void Close(OpenXmlCompositeElement chart, ChartType type, bool threeDimensional)
    {
        chart.AppendChild(new C.FirstSliceAngle { Val = 0 });
        chart.AppendChild(new C.HoleSize { Val = 50 });
    }

    public override OpenXmlCompositeElement Series() => new C.PieChartSeries();

    public override bool ColoursPoints => true;

    public override bool HasAxes => false;

    public override ChartFamilyRead? Read(C.PlotArea plot) =>
        plot.GetFirstChild<C.DoughnutChart>() is { } doughnut ? new ChartFamilyRead(ChartType.Doughnut, [.. doughnut.Elements<C.PieChartSeries>()]) : null;
}

internal sealed class ScatterFamily : ChartFamily
{
    public override bool Writes(ChartType type) => type is ChartType.Scatter or ChartType.ScatterWithLines;

    public override bool ValueCategoryAxis => true;

    public override bool DrawsLines(ChartType type) => true;

    public override OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional) =>
        new C.ScatterChart(new C.ScatterStyle { Val = type == ChartType.Scatter ? C.ScatterStyleValues.Marker : C.ScatterStyleValues.LineMarker }, new C.VaryColors { Val = false });

    public override OpenXmlCompositeElement Series() => new C.ScatterChartSeries();

    public override OpenXmlElement Categories(OpenXmlElement categories) => Moved(new C.XValues(), categories);

    public override OpenXmlElement Values(OpenXmlElement values) => Moved(new C.YValues(), values);

    public override void Finish(OpenXmlCompositeElement series, ChartDefinition definition)
    {
        if (definition.Type == ChartType.Scatter && series.GetFirstChild<C.ChartShapeProperties>() is { } properties)
        {
            var colour = properties.Descendants<A.RgbColorModelHex>().FirstOrDefault()?.Val?.Value ?? "4472C4";
            properties.RemoveAllChildren();
            properties.AppendChild(new A.Outline(new A.NoFill()) { Width = 28575 });
            properties.InsertAfterSelf(new C.Marker(
                new C.Symbol { Val = C.MarkerStyleValues.Circle },
                new C.Size { Val = 7 },
                new C.ChartShapeProperties(new A.SolidFill(new A.RgbColorModelHex { Val = colour }))));
        }
        series.AppendChild(new C.Smooth { Val = false });
    }

    public override ChartFamilyRead? Read(C.PlotArea plot)
    {
        if (plot.GetFirstChild<C.ScatterChart>() is not { } scatter) return null;
        var series = scatter.Elements<C.ScatterChartSeries>().ToList();
        var style = scatter.GetFirstChild<C.ScatterStyle>()?.Val?.Value;
        var noLines = series.Count > 0 && series.All(element => element.GetFirstChild<C.ChartShapeProperties>()?.GetFirstChild<A.Outline>()?.GetFirstChild<A.NoFill>() is not null);
        var markersOnly = noLines || style == C.ScatterStyleValues.Marker;
        return new ChartFamilyRead(markersOnly ? ChartType.Scatter : ChartType.ScatterWithLines, series);
    }

    private OpenXmlElement Moved(OpenXmlCompositeElement target, OpenXmlElement source)
    {
        foreach (var child in source.ChildElements) target.AppendChild(child.CloneNode(true));
        return target;
    }
}

internal sealed class BubbleFamily : ChartFamily
{
    public override bool Writes(ChartType type) => type is ChartType.Bubble;

    public override bool ValueCategoryAxis => true;

    public override OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional) => new C.BubbleChart(new C.VaryColors { Val = false });

    public override OpenXmlCompositeElement Series() => new C.BubbleChartSeries();

    public override IReadOnlyList<IReadOnlyList<int>> Bundles(IReadOnlyList<int> members) => members.Count == 0 ? [] : [members];

    public override int Lead(IReadOnlyList<int> bundle) => bundle.Count > 1 ? bundle[1] : bundle[0];

    public override IEnumerable<OpenXmlElement> Data(ISeriesSource source, ChartData data, IReadOnlyList<int> bundle)
    {
        var x = bundle.Count > 1 ? Moved(new C.XValues(), source.Values(bundle[0], data.Series[bundle[0]].Values)) : Moved(new C.XValues(), source.Categories(data.Categories));
        var y = Lead(bundle);
        var size = bundle.Count > 2 ? bundle[2] : y;
        return [x, Moved(new C.YValues(), source.Values(y, data.Series[y].Values)), Moved(new C.BubbleSize(), source.Values(size, data.Series[size].Values))];
    }

    public override void Finish(OpenXmlCompositeElement series, ChartDefinition definition)
    {
        InsertAfterProperties(series, new C.InvertIfNegative { Val = false });
        series.AppendChild(new C.Bubble3D { Val = false });
    }

    public override void Close(OpenXmlCompositeElement chart, ChartType type, bool threeDimensional)
    {
        chart.AppendChild(new C.BubbleScale { Val = 100 });
        chart.AppendChild(new C.ShowNegativeBubbles { Val = false });
    }

    public override ChartFamilyRead? Read(C.PlotArea plot) =>
        plot.GetFirstChild<C.BubbleChart>() is { } bubble ? new ChartFamilyRead(ChartType.Bubble, [.. bubble.Elements<C.BubbleChartSeries>()]) : null;

    private OpenXmlElement Moved(OpenXmlCompositeElement target, OpenXmlElement source)
    {
        var numbers = source.GetFirstChild<C.NumberReference>() ?? (OpenXmlElement?)source.GetFirstChild<C.NumberLiteral>();
        if (numbers is not null) target.AppendChild(numbers.CloneNode(true));
        else foreach (var child in source.ChildElements) target.AppendChild(child.CloneNode(true));
        return target;
    }
}

internal sealed class StockFamily : ChartFamily
{
    public override bool Writes(ChartType type) => type is ChartType.StockHighLowClose or ChartType.StockOpenHighLowClose or ChartType.Candlestick;

    public override OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional) => new C.StockChart();

    public override bool DeclaresType => true;

    public override OpenXmlCompositeElement Series() => new C.LineChartSeries();

    public override void Finish(OpenXmlCompositeElement series, ChartDefinition definition)
    {
        if (series.GetFirstChild<C.ChartShapeProperties>() is { } properties)
        {
            properties.RemoveAllChildren();
            properties.AppendChild(new A.Outline(new A.NoFill()) { Width = 28575 });
        }
        InsertAfterProperties(series, new C.Marker(new C.Symbol { Val = C.MarkerStyleValues.None }));
        series.AppendChild(new C.Smooth { Val = false });
    }

    public override void Close(OpenXmlCompositeElement chart, ChartType type, bool threeDimensional)
    {
        chart.AppendChild(new C.HighLowLines());
        if (type == ChartType.Candlestick && chart.Elements<C.LineChartSeries>().Count() >= 4)
            chart.AppendChild(new C.UpDownBars(new C.GapWidth { Val = 150 }, new C.UpBars(), new C.DownBars()));
    }

    public override ChartFamilyRead? Read(C.PlotArea plot)
    {
        if (plot.GetFirstChild<C.StockChart>() is not { } stock) return null;
        var series = stock.Elements<C.LineChartSeries>().ToList();
        var type = series.Count < 4 ? ChartType.StockHighLowClose
            : stock.GetFirstChild<C.UpDownBars>() is not null ? ChartType.Candlestick
            : ChartType.StockOpenHighLowClose;
        return new ChartFamilyRead(type, series);
    }
}

internal sealed class RadarFamily : ChartFamily
{
    public override bool Writes(ChartType type) => type is ChartType.Radar or ChartType.FilledRadar;

    public override bool DrawsLines(ChartType type) => type == ChartType.Radar;

    public override OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional) =>
        new C.RadarChart(new C.RadarStyle { Val = type == ChartType.FilledRadar ? C.RadarStyleValues.Filled : C.RadarStyleValues.Marker }, new C.VaryColors { Val = false });

    public override OpenXmlCompositeElement Series() => new C.RadarChartSeries();

    public override ChartFamilyRead? Read(C.PlotArea plot) =>
        plot.GetFirstChild<C.RadarChart>() is { } radar
            ? new ChartFamilyRead(radar.GetFirstChild<C.RadarStyle>()?.Val?.Value == C.RadarStyleValues.Filled ? ChartType.FilledRadar : ChartType.Radar, [.. radar.Elements<C.RadarChartSeries>()])
            : null;
}

internal sealed class ColumnLineFamily : ChartFamily
{
    public override bool Writes(ChartType type) => false;

    public override OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional) => throw new NotSupportedException();

    public override OpenXmlCompositeElement Series() => throw new NotSupportedException();

    public override ChartFamilyRead? Read(C.PlotArea plot)
    {
        if (plot.GetFirstChild<C.BarChart>() is not { } bar || plot.GetFirstChild<C.LineChart>() is not { } line) return null;
        if (bar.GetFirstChild<C.BarDirection>()?.Val?.Value == C.BarDirectionValues.Bar) return null;
        if (bar.GetFirstChild<C.BarGrouping>()?.Val?.Value is { } grouping && grouping != C.BarGroupingValues.Clustered) return null;

        return new ChartFamilyRead(ChartType.ColumnLine, [.. bar.Elements<C.BarChartSeries>(), .. line.Elements<C.LineChartSeries>()]);
    }
}

internal sealed class SurfaceFamily : ChartFamily
{
    public override bool Writes(ChartType type) => false;

    public override OpenXmlCompositeElement Chart(ChartType type, bool threeDimensional) => throw new NotSupportedException();

    public override OpenXmlCompositeElement Series() => throw new NotSupportedException();

    public override ChartFamilyRead? Read(C.PlotArea plot) =>
        plot.GetFirstChild<C.Surface3DChart>() is { } solid ? new ChartFamilyRead(ChartType.Surface3D, [.. solid.Elements<C.SurfaceChartSeries>()]) { ThreeDimensional = true }
        : plot.GetFirstChild<C.SurfaceChart>() is { } flat ? new ChartFamilyRead(ChartType.Surface, [.. flat.Elements<C.SurfaceChartSeries>()])
        : null;
}

internal sealed class ChartFamilies
{
    private readonly IReadOnlyList<ChartFamily> _families =
    [
        new ColumnLineFamily(), new PieFamily(), new OfPieFamily(), new DoughnutFamily(), new ScatterFamily(), new BubbleFamily(), new StockFamily(), new RadarFamily(),
        new LineFamily(), new AreaFamily(), new BarFamily(), new SurfaceFamily(),
    ];

    public ChartFamily For(ChartType type) => _families.First(family => family.Writes(type));

    public bool IsNative(ChartType type) => type != ChartType.SteppedLine && (type == ChartType.ColumnLine || _families.Any(family => family is not BarFamily && family.Writes(type))
        || type is ChartType.Column or ChartType.StackedColumn or ChartType.PercentStackedColumn or ChartType.Bar or ChartType.StackedBar or ChartType.PercentStackedBar);

    public bool Declares(ChartType type) => !IsNative(type) || For(type).DeclaresType;

    public bool Refines(ChartType declared, ChartType read) =>
        IsNative(declared) ? For(declared).DeclaresType && For(declared).Writes(read) : read == WrittenAs(declared);

    public ChartType WrittenAs(ChartType type) => For(type) is LineFamily ? ChartType.Line : ChartType.Column;

    public bool KeepsThreeDimensional(ChartType type) => type != ChartType.ColumnLine && For(type).WritesThreeDimensional(type);

    public ChartFamilyRead? Read(C.PlotArea plot) =>
        _families.Select(family => family.Read(plot)).FirstOrDefault(found => found is not null);
}

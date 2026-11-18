using DocumentFormat.OpenXml;
using PulseOffice.Charts;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlAxisScales
{
    public C.Scaling Scaling(ChartAxisScale scale)
    {
        var scaling = new C.Scaling();
        if (scale.Logarithmic) scaling.AppendChild(new C.LogBase { Val = scale.EffectiveLogBase });
        scaling.AppendChild(new C.Orientation { Val = scale.Reversed ? C.OrientationValues.MaxMin : C.OrientationValues.MinMax });
        if (scale.Maximum is { } maximum && double.IsFinite(maximum)) scaling.AppendChild(new C.MaxAxisValue { Val = maximum });
        if (scale.Minimum is { } minimum && double.IsFinite(minimum)) scaling.AppendChild(new C.MinAxisValue { Val = minimum });
        return scaling;
    }

    public IEnumerable<OpenXmlElement> NumberFormat(ChartAxisScale scale) =>
        scale.NumberFormat is { Length: > 0 } code ? [new C.NumberingFormat { FormatCode = code, SourceLinked = false }] : [];

    public OpenXmlElement Crossing(ChartAxisScale crossed) => crossed.Crossing switch
    {
        AxisCrossing.Minimum => new C.Crosses { Val = C.CrossesValues.Minimum },
        AxisCrossing.Maximum => new C.Crosses { Val = C.CrossesValues.Maximum },
        AxisCrossing.Value when double.IsFinite(crossed.CrossingValue) => new C.CrossesAt { Val = crossed.CrossingValue },
        _ => new C.Crosses { Val = C.CrossesValues.AutoZero },
    };

    public IEnumerable<OpenXmlElement> Units(ChartAxisScale scale)
    {
        if (scale.MajorUnit is { } major && major > 0 && double.IsFinite(major)) yield return new C.MajorUnit { Val = major };
        if (scale.MinorUnit is { } minor && minor > 0 && double.IsFinite(minor)) yield return new C.MinorUnit { Val = minor };
    }

    public ChartAxisScale Read(OpenXmlCompositeElement? axis, C.PlotArea plot)
    {
        if (axis is null) return ChartAxisScale.Automatic;
        var scaling = axis.GetFirstChild<C.Scaling>();
        var id = axis.GetFirstChild<C.AxisId>()?.Val?.Value;
        var crossing = id is null ? null : plot.ChildElements.OfType<OpenXmlCompositeElement>()
            .FirstOrDefault(other => other != axis && other.GetFirstChild<C.CrossingAxis>()?.Val?.Value == id);
        var crosses = crossing?.GetFirstChild<C.Crosses>()?.Val?.Value;
        var crossesAt = crossing?.GetFirstChild<C.CrossesAt>()?.Val?.Value;
        var logBase = scaling?.GetFirstChild<C.LogBase>()?.Val?.Value;
        var scale = new ChartAxisScale
        {
            Minimum = Finite(scaling?.GetFirstChild<C.MinAxisValue>()?.Val?.Value),
            Maximum = Finite(scaling?.GetFirstChild<C.MaxAxisValue>()?.Val?.Value),
            MajorUnit = Positive(axis.GetFirstChild<C.MajorUnit>()?.Val?.Value),
            MinorUnit = Positive(axis.GetFirstChild<C.MinorUnit>()?.Val?.Value),
            Logarithmic = logBase is not null,
            LogBase = logBase is > 1 and var value && double.IsFinite(value) ? value : 10,
            Reversed = scaling?.GetFirstChild<C.Orientation>()?.Val?.Value == C.OrientationValues.MaxMin,
            Crossing = crossesAt is { } at && double.IsFinite(at) ? AxisCrossing.Value
                : crosses == C.CrossesValues.Minimum ? AxisCrossing.Minimum
                : crosses == C.CrossesValues.Maximum ? AxisCrossing.Maximum
                : AxisCrossing.Automatic,
            CrossingValue = crossesAt is { } point && double.IsFinite(point) ? point : 0,
            NumberFormat = axis.GetFirstChild<C.NumberingFormat>() is { } format && format.SourceLinked?.Value != true && format.FormatCode?.Value is { Length: > 0 } code && code != "General" ? code : null,
        };
        return scale.IsAutomatic ? ChartAxisScale.Automatic : scale;
    }

    private double? Finite(double? value) => value is { } number && double.IsFinite(number) ? number : null;

    private double? Positive(double? value) => value is > 0 and var number && double.IsFinite(number) ? number : null;
}

internal sealed class OoxmlDataLabels
{
    private readonly (DataLabelPlacement Placement, C.DataLabelPositionValues Value)[] _placements =
    [
        (DataLabelPlacement.Center, C.DataLabelPositionValues.Center),
        (DataLabelPlacement.InsideEnd, C.DataLabelPositionValues.InsideEnd),
        (DataLabelPlacement.InsideBase, C.DataLabelPositionValues.InsideBase),
        (DataLabelPlacement.OutsideEnd, C.DataLabelPositionValues.OutsideEnd),
        (DataLabelPlacement.Above, C.DataLabelPositionValues.Top),
        (DataLabelPlacement.Below, C.DataLabelPositionValues.Bottom),
        (DataLabelPlacement.Left, C.DataLabelPositionValues.Left),
        (DataLabelPlacement.Right, C.DataLabelPositionValues.Right),
    ];

    public C.DataLabels Write(ChartDataLabelOptions options, OpenXmlCompositeElement series)
    {
        var labels = new C.DataLabels();
        if (options.NumberFormat is { Length: > 0 } code) labels.AppendChild(new C.NumberingFormat { FormatCode = code, SourceLinked = false });
        if (options.Placement != DataLabelPlacement.Automatic && Allows(series, options.Placement))
            labels.AppendChild(new C.DataLabelPosition { Val = _placements.First(pair => pair.Placement == options.Placement).Value });
        labels.Append(
            new C.ShowLegendKey { Val = false },
            new C.ShowValue { Val = options.ShowValue },
            new C.ShowCategoryName { Val = options.ShowCategory },
            new C.ShowSeriesName { Val = options.ShowSeriesName },
            new C.ShowPercent { Val = options.ShowPercent },
            new C.ShowBubbleSize { Val = false });
        return labels;
    }

    public bool Allows(OpenXmlCompositeElement series, DataLabelPlacement placement) => series switch
    {
        C.BarChartSeries => placement is DataLabelPlacement.Center or DataLabelPlacement.InsideEnd or DataLabelPlacement.InsideBase or DataLabelPlacement.OutsideEnd,
        C.PieChartSeries => placement is DataLabelPlacement.Center or DataLabelPlacement.InsideEnd or DataLabelPlacement.OutsideEnd,
        C.LineChartSeries or C.ScatterChartSeries or C.BubbleChartSeries => placement is DataLabelPlacement.Center or DataLabelPlacement.Above or DataLabelPlacement.Below or DataLabelPlacement.Left or DataLabelPlacement.Right,
        _ => false,
    };

    public bool Shown(C.DataLabels? labels) =>
        labels is not null && (labels.GetFirstChild<C.ShowValue>()?.Val?.Value == true || labels.GetFirstChild<C.ShowCategoryName>()?.Val?.Value == true
            || labels.GetFirstChild<C.ShowSeriesName>()?.Val?.Value == true || labels.GetFirstChild<C.ShowPercent>()?.Val?.Value == true);

    public ChartDataLabelOptions Read(C.DataLabels? labels)
    {
        if (labels is null) return ChartDataLabelOptions.ValuesOnly;
        var position = labels.GetFirstChild<C.DataLabelPosition>()?.Val?.Value;
        var options = new ChartDataLabelOptions
        {
            ShowValue = labels.GetFirstChild<C.ShowValue>()?.Val?.Value == true,
            ShowCategory = labels.GetFirstChild<C.ShowCategoryName>()?.Val?.Value == true,
            ShowSeriesName = labels.GetFirstChild<C.ShowSeriesName>()?.Val?.Value == true,
            ShowPercent = labels.GetFirstChild<C.ShowPercent>()?.Val?.Value == true,
            NumberFormat = labels.GetFirstChild<C.NumberingFormat>() is { } format && format.SourceLinked?.Value != true && format.FormatCode?.Value is { Length: > 0 } code && code != "General" ? code : null,
            Placement = position is null ? DataLabelPlacement.Automatic
                : _placements.Where(pair => pair.Value == position.Value).Select(pair => pair.Placement).DefaultIfEmpty(DataLabelPlacement.Automatic).First(),
        };
        return options.IsValuesOnly ? ChartDataLabelOptions.ValuesOnly : options;
    }
}

internal sealed class OoxmlSeriesFormats
{
    private const long DefaultLineWidth = 28575;

    private readonly (TrendlineKind Kind, C.TrendlineValues Value)[] _trendlines =
    [
        (TrendlineKind.Linear, C.TrendlineValues.Linear),
        (TrendlineKind.Exponential, C.TrendlineValues.Exponential),
        (TrendlineKind.Logarithmic, C.TrendlineValues.Logarithmic),
        (TrendlineKind.Polynomial, C.TrendlineValues.Polynomial),
        (TrendlineKind.Power, C.TrendlineValues.Power),
        (TrendlineKind.MovingAverage, C.TrendlineValues.MovingAverage),
    ];

    private readonly (ErrorBarKind Kind, C.ErrorValues Value)[] _errorKinds =
    [
        (ErrorBarKind.FixedValue, C.ErrorValues.FixedValue),
        (ErrorBarKind.Percentage, C.ErrorValues.Percentage),
        (ErrorBarKind.StandardDeviation, C.ErrorValues.StandardDeviation),
        (ErrorBarKind.StandardError, C.ErrorValues.StandardError),
    ];

    private readonly (MarkerSymbol Symbol, C.MarkerStyleValues Value)[] _markers =
    [
        (MarkerSymbol.None, C.MarkerStyleValues.None),
        (MarkerSymbol.Circle, C.MarkerStyleValues.Circle),
        (MarkerSymbol.Square, C.MarkerStyleValues.Square),
        (MarkerSymbol.Diamond, C.MarkerStyleValues.Diamond),
        (MarkerSymbol.Triangle, C.MarkerStyleValues.Triangle),
        (MarkerSymbol.Cross, C.MarkerStyleValues.X),
        (MarkerSymbol.Star, C.MarkerStyleValues.Star),
        (MarkerSymbol.Plus, C.MarkerStyleValues.Plus),
        (MarkerSymbol.Dash, C.MarkerStyleValues.Dash),
        (MarkerSymbol.Dot, C.MarkerStyleValues.Dot),
    ];

    private readonly (LineDash Dash, A.PresetLineDashValues Value)[] _dashes =
    [
        (LineDash.Dash, A.PresetLineDashValues.Dash),
        (LineDash.Dot, A.PresetLineDashValues.SystemDot),
        (LineDash.DashDot, A.PresetLineDashValues.DashDot),
    ];

    public bool CarriesMarkers(OpenXmlCompositeElement series) => series is C.LineChartSeries or C.ScatterChartSeries or C.RadarChartSeries;

    public bool CarriesTrendlines(OpenXmlCompositeElement series) =>
        series is C.BarChartSeries or C.LineChartSeries or C.AreaChartSeries or C.ScatterChartSeries or C.BubbleChartSeries;

    public void Apply(OpenXmlCompositeElement series, ChartSeriesFormat format)
    {
        if (format.IsPlain) return;
        Line(series, format);
        if (format.Marker != MarkerSymbol.Automatic && CarriesMarkers(series)) Marker(series, format);
        if (!CarriesTrendlines(series)) return;
        if (format.Trendline is { } trendline) BeforeData(series, Trendline(trendline));
        if (format.ErrorBars is { } bars) BeforeData(series, ErrorBars(bars, series is C.ScatterChartSeries or C.BubbleChartSeries));
    }

    private void Line(OpenXmlCompositeElement series, ChartSeriesFormat format)
    {
        if (format.LineWidthPoints is null && format.Dash == LineDash.Solid) return;
        var properties = series.GetFirstChild<C.ChartShapeProperties>();
        if (properties is null)
        {
            properties = new C.ChartShapeProperties();
            if (series.GetFirstChild<C.SeriesText>() is { } text) text.InsertAfterSelf(properties);
            else series.PrependChild(properties);
        }
        var outline = properties.GetFirstChild<A.Outline>() ?? properties.AppendChild(new A.Outline());
        outline.Width = format.LineWidthPoints is { } points && points > 0 && double.IsFinite(points) ? (int)PulseOffice.Foundation.Units.Emu.FromPoints(points).Value : outline.Width?.Value ?? (int)DefaultLineWidth;
        outline.RemoveAllChildren<A.PresetDash>();
        if (format.Dash != LineDash.Solid)
        {
            var dash = new A.PresetDash { Val = _dashes.First(pair => pair.Dash == format.Dash).Value };
            if (outline.ChildElements.FirstOrDefault(child => child is A.NoFill or A.SolidFill or A.GradientFill or A.PatternFill) is { } fill) fill.InsertAfterSelf(dash);
            else outline.PrependChild(dash);
        }
    }

    private void Marker(OpenXmlCompositeElement series, ChartSeriesFormat format)
    {
        var existing = series.GetFirstChild<C.Marker>();
        var marker = new C.Marker(new C.Symbol { Val = _markers.First(pair => pair.Symbol == format.Marker).Value });
        if (format.MarkerSize is { } size && format.Marker != MarkerSymbol.None) marker.AppendChild(new C.Size { Val = (byte)Math.Clamp(size, 2, 72) });
        if (existing?.GetFirstChild<C.ChartShapeProperties>() is { } fill && format.Marker != MarkerSymbol.None) marker.AppendChild(fill.CloneNode(true));
        if (existing is not null)
        {
            existing.InsertAfterSelf(marker);
            existing.Remove();
        }
        else if (series.GetFirstChild<C.ChartShapeProperties>() is { } properties) properties.InsertAfterSelf(marker);
        else if (series.GetFirstChild<C.SeriesText>() is { } text) text.InsertAfterSelf(marker);
        else series.PrependChild(marker);
    }

    private void BeforeData(OpenXmlCompositeElement series, OpenXmlElement element)
    {
        var data = series.ChildElements.FirstOrDefault(child => child is C.CategoryAxisData or C.Values or C.XValues or C.YValues or C.BubbleSize);
        if (data is not null) data.InsertBeforeSelf(element);
        else series.AppendChild(element);
    }

    private C.Trendline Trendline(ChartTrendline trendline)
    {
        var element = new C.Trendline();
        if (trendline.Name is { Length: > 0 } name) element.AppendChild(new C.TrendlineName(name));
        element.AppendChild(new C.TrendlineType { Val = _trendlines.First(pair => pair.Kind == trendline.Kind).Value });
        if (trendline.Kind == TrendlineKind.Polynomial) element.AppendChild(new C.PolynomialOrder { Val = (byte)trendline.EffectiveOrder });
        if (trendline.Kind == TrendlineKind.MovingAverage) element.AppendChild(new C.Period { Val = (uint)trendline.EffectivePeriod });
        if (trendline.Forward > 0 && double.IsFinite(trendline.Forward)) element.AppendChild(new C.Forward { Val = trendline.Forward });
        if (trendline.Backward > 0 && double.IsFinite(trendline.Backward)) element.AppendChild(new C.Backward { Val = trendline.Backward });
        if (trendline.Intercept is { } intercept && double.IsFinite(intercept)) element.AppendChild(new C.Intercept { Val = intercept });
        element.AppendChild(new C.DisplayRSquaredValue { Val = trendline.DisplayRSquared });
        element.AppendChild(new C.DisplayEquation { Val = trendline.DisplayEquation });
        return element;
    }

    private C.ErrorBars ErrorBars(ChartErrorBars bars, bool directed)
    {
        var element = new C.ErrorBars();
        if (directed) element.AppendChild(new C.ErrorDirection { Val = C.ErrorBarDirectionValues.Y });
        element.AppendChild(new C.ErrorBarType
        {
            Val = bars.Direction switch
            {
                ErrorBarDirection.Plus => C.ErrorBarValues.Plus,
                ErrorBarDirection.Minus => C.ErrorBarValues.Minus,
                _ => C.ErrorBarValues.Both,
            },
        });
        element.AppendChild(new C.ErrorBarValueType { Val = _errorKinds.First(pair => pair.Kind == bars.Kind).Value });
        element.AppendChild(new C.NoEndCap { Val = !bars.EndCaps });
        if (bars.Kind != ErrorBarKind.StandardError) element.AppendChild(new C.ErrorBarValue { Val = double.IsFinite(bars.Value) ? bars.Value : 1 });
        return element;
    }

    public ChartSeriesFormat Read(OpenXmlCompositeElement? series, bool secondary)
    {
        if (series is null) return ChartSeriesFormat.Plain;
        var outline = series.GetFirstChild<C.ChartShapeProperties>()?.GetFirstChild<A.Outline>();
        var width = outline?.Width?.Value;
        var dash = outline?.GetFirstChild<A.PresetDash>()?.Val?.Value;
        var marker = series.GetFirstChild<C.Marker>();
        var symbol = marker?.GetFirstChild<C.Symbol>()?.Val?.Value;
        var format = new ChartSeriesFormat
        {
            LineWidthPoints = width is { } emu && emu > 0 && emu != DefaultLineWidth ? emu / (double)PulseOffice.Foundation.Units.Emu.PerPoint : null,
            Dash = dash is null ? LineDash.Solid : Dash(dash.Value),
            Marker = symbol is null || !CarriesMarkers(series) ? MarkerSymbol.Automatic : Symbol(symbol.Value, series),
            MarkerSize = marker?.GetFirstChild<C.Size>()?.Val?.Value is { } size && size != 7 ? size : null,
            SecondaryAxis = secondary,
            Trendline = series.GetFirstChild<C.Trendline>() is { } trendline ? Trendline(trendline) : null,
            ErrorBars = series.Elements<C.ErrorBars>().FirstOrDefault(bars => bars.GetFirstChild<C.ErrorDirection>()?.Val?.Value != C.ErrorBarDirectionValues.X) is { } bars ? ErrorBars(bars) : null,
        };
        return format.IsPlain ? ChartSeriesFormat.Plain : format;
    }

    private LineDash Dash(A.PresetLineDashValues value) =>
        value == A.PresetLineDashValues.Solid ? LineDash.Solid
        : value == A.PresetLineDashValues.Dot || value == A.PresetLineDashValues.SystemDot ? LineDash.Dot
        : value == A.PresetLineDashValues.DashDot || value == A.PresetLineDashValues.SystemDashDot || value == A.PresetLineDashValues.LargeDashDot
            || value == A.PresetLineDashValues.SystemDashDotDot || value == A.PresetLineDashValues.LargeDashDotDot ? LineDash.DashDot
        : LineDash.Dash;

    private MarkerSymbol Symbol(C.MarkerStyleValues value, OpenXmlCompositeElement series)
    {
        if (value == C.MarkerStyleValues.None && series.Parent is C.StockChart) return MarkerSymbol.Automatic;
        if (value == C.MarkerStyleValues.Circle && series is C.ScatterChartSeries && series.GetFirstChild<C.Marker>()?.GetFirstChild<C.Size>()?.Val?.Value == 7) return MarkerSymbol.Automatic;
        return _markers.Where(pair => pair.Value == value).Select(pair => pair.Symbol).DefaultIfEmpty(MarkerSymbol.Automatic).First();
    }

    private ChartTrendline Trendline(C.Trendline element)
    {
        var type = element.GetFirstChild<C.TrendlineType>()?.Val?.Value;
        var kind = type is null ? TrendlineKind.Linear
            : _trendlines.Where(pair => pair.Value == type.Value).Select(pair => pair.Kind).DefaultIfEmpty(TrendlineKind.Linear).First();
        return new ChartTrendline(kind)
        {
            Order = element.GetFirstChild<C.PolynomialOrder>()?.Val?.Value is { } order ? order : 2,
            Period = element.GetFirstChild<C.Period>()?.Val?.Value is { } period ? (int)Math.Min(period, 255u) : 2,
            Forward = element.GetFirstChild<C.Forward>()?.Val?.Value is { } forward && double.IsFinite(forward) ? forward : 0,
            Backward = element.GetFirstChild<C.Backward>()?.Val?.Value is { } backward && double.IsFinite(backward) ? backward : 0,
            Intercept = element.GetFirstChild<C.Intercept>()?.Val?.Value is { } intercept && double.IsFinite(intercept) ? intercept : null,
            DisplayEquation = element.GetFirstChild<C.DisplayEquation>()?.Val?.Value == true,
            DisplayRSquared = element.GetFirstChild<C.DisplayRSquaredValue>()?.Val?.Value == true,
            Name = element.GetFirstChild<C.TrendlineName>()?.Text is { Length: > 0 } name ? name : null,
        };
    }

    private ChartErrorBars ErrorBars(C.ErrorBars element)
    {
        var type = element.GetFirstChild<C.ErrorBarValueType>()?.Val?.Value;
        var kind = type is null ? ErrorBarKind.FixedValue
            : _errorKinds.Where(pair => pair.Value == type.Value).Select(pair => pair.Kind).DefaultIfEmpty(ErrorBarKind.FixedValue).First();
        var direction = element.GetFirstChild<C.ErrorBarType>()?.Val?.Value;
        return new ChartErrorBars(kind, element.GetFirstChild<C.ErrorBarValue>()?.Val?.Value is { } value && double.IsFinite(value) ? value : 1)
        {
            Direction = direction == C.ErrorBarValues.Plus ? ErrorBarDirection.Plus : direction == C.ErrorBarValues.Minus ? ErrorBarDirection.Minus : ErrorBarDirection.Both,
            EndCaps = element.GetFirstChild<C.NoEndCap>()?.Val?.Value != true,
        };
    }

    public bool UsesCustomErrorValues(OpenXmlCompositeElement series) =>
        series.Elements<C.ErrorBars>().Any(bars => bars.GetFirstChild<C.ErrorBarValueType>()?.Val?.Value == C.ErrorValues.Custom);
}

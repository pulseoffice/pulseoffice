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
        (DataLabelPlacement.BestFit, C.DataLabelPositionValues.BestFit),
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
        C.PieChartSeries => placement is DataLabelPlacement.Center or DataLabelPlacement.InsideEnd or DataLabelPlacement.OutsideEnd or DataLabelPlacement.BestFit,
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
        (ErrorBarKind.Custom, C.ErrorValues.Custom),
    ];

    private readonly OoxmlRangeFormulas _formulas = new();

    private readonly OoxmlPointFormats _points = new();

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
        _points.Apply(series, format.Points);
        if (!CarriesTrendlines(series)) return;
        if (format.Trendline is { } trendline) BeforeData(series, Trendline(trendline));
        var directed = series is C.ScatterChartSeries or C.BubbleChartSeries;
        if (format.ErrorBars is { } bars) BeforeData(series, ErrorBars(bars, directed ? C.ErrorBarDirectionValues.Y : null));
        if (format.XErrorBars is { } across && directed) BeforeData(series, ErrorBars(across, C.ErrorBarDirectionValues.X));
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

    private C.ErrorBars ErrorBars(ChartErrorBars bars, C.ErrorBarDirectionValues? direction)
    {
        var element = new C.ErrorBars();
        if (direction is { } dimension) element.AppendChild(new C.ErrorDirection { Val = dimension });
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
        if (bars.Kind == ErrorBarKind.Custom)
        {
            element.AppendChild(new C.Plus(CustomValues(bars.Plus)));
            element.AppendChild(new C.Minus(CustomValues(bars.Minus)));
            return element;
        }
        if (bars.Kind != ErrorBarKind.StandardError) element.AppendChild(new C.ErrorBarValue { Val = double.IsFinite(bars.Value) ? bars.Value : 1 });
        return element;
    }

    private OpenXmlElement CustomValues(ChartErrorValues? values)
    {
        var points = values?.Values ?? [];
        var cache = Enumerable.Range(0, points.Count).Where(index => double.IsFinite(points[index]))
            .Select(index => new C.NumericPoint(new C.NumericValue(points[index].ToString("R", System.Globalization.CultureInfo.InvariantCulture))) { Index = (uint)index });
        if (_formulas.Formula(values?.Reference) is { } formula)
        {
            var numbers = new C.NumberingCache(new C.FormatCode("General"), new C.PointCount { Val = (uint)points.Count });
            foreach (var point in cache) numbers.AppendChild(point);
            return new C.NumberReference(new C.Formula(formula), numbers);
        }
        var literal = new C.NumberLiteral(new C.FormatCode("General"), new C.PointCount { Val = (uint)points.Count });
        foreach (var point in cache) literal.AppendChild(point);
        return literal;
    }

    private ChartErrorValues CustomValues(OpenXmlCompositeElement? side)
    {
        if (side is null) return ChartErrorValues.Empty;
        var reference = side.GetFirstChild<C.NumberReference>();
        var points = (OpenXmlCompositeElement?)reference?.NumberingCache ?? side.GetFirstChild<C.NumberLiteral>();
        var count = (int)Math.Min((points?.GetFirstChild<C.PointCount>()?.Val?.Value ?? 0u), 100000u);
        var values = Enumerable.Repeat(double.NaN, count).ToArray();
        foreach (var point in points?.Elements<C.NumericPoint>() ?? [])
            if (point.Index?.Value is { } index && index < count
                && double.TryParse(point.NumericValue?.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
                values[index] = value;
        return new ChartErrorValues(_formulas.Model(reference?.Formula?.Text), values);
    }

    public ChartSeriesFormat Read(OpenXmlCompositeElement? series, bool secondary)
    {
        if (series is null) return ChartSeriesFormat.Plain;
        var outline = series.GetFirstChild<C.ChartShapeProperties>()?.GetFirstChild<A.Outline>();
        var width = outline?.Width?.Value;
        var dash = outline?.GetFirstChild<A.PresetDash>()?.Val?.Value;
        var marker = series.GetFirstChild<C.Marker>();
        var symbol = marker?.GetFirstChild<C.Symbol>()?.Val?.Value;
        var shape = symbol is null || !CarriesMarkers(series) ? MarkerSymbol.Automatic : Symbol(symbol.Value, series);
        var format = new ChartSeriesFormat
        {
            LineWidthPoints = width is { } emu && emu > 0 && emu != DefaultLineWidth ? emu / (double)PulseOffice.Foundation.Units.Emu.PerPoint : null,
            Dash = dash is null ? LineDash.Solid : Dash(dash.Value),
            Marker = shape,
            MarkerSize = shape != MarkerSymbol.Automatic && marker?.GetFirstChild<C.Size>()?.Val?.Value is { } size && size != 7 ? size : null,
            SecondaryAxis = secondary,
            Trendline = series.GetFirstChild<C.Trendline>() is { } trendline ? Trendline(trendline) : null,
            ErrorBars = series.Elements<C.ErrorBars>().FirstOrDefault(bars => bars.GetFirstChild<C.ErrorDirection>()?.Val?.Value != C.ErrorBarDirectionValues.X) is { } bars ? ErrorBars(bars) : null,
            XErrorBars = series is C.ScatterChartSeries or C.BubbleChartSeries
                && series.Elements<C.ErrorBars>().FirstOrDefault(bars => bars.GetFirstChild<C.ErrorDirection>()?.Val?.Value == C.ErrorBarDirectionValues.X) is { } across
                    ? ErrorBars(across) : null,
            Points = _points.Read(series),
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
        if (value == C.MarkerStyleValues.Dash && series.Parent is C.StockChart stock && stock.GetFirstChild<C.UpDownBars>() is null) return MarkerSymbol.Automatic;
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
            Plus = kind == ErrorBarKind.Custom ? CustomValues(element.GetFirstChild<C.Plus>()) : null,
            Minus = kind == ErrorBarKind.Custom ? CustomValues(element.GetFirstChild<C.Minus>()) : null,
        };
    }
}

internal sealed class OoxmlPointFormats
{
    private readonly ChartPalette _palette = new();
    private readonly ChartThemeColours _themes = new();
    private readonly ChartFillColour _fills = new();

    public void Apply(OpenXmlCompositeElement series, ChartPointFormats points)
    {
        foreach (var point in points.Items)
        {
            var element = Find(series, point.Index) ?? Insert(series, point.Index);
            if (point.ExplosionPercent > 0)
            {
                element.RemoveAllChildren<C.Explosion>();
                var explosion = new C.Explosion { Val = (uint)point.ExplosionPercent };
                var before = (OpenXmlElement?)element.GetFirstChild<C.Bubble3D>() ?? (OpenXmlElement?)element.GetFirstChild<C.Marker>() ?? (OpenXmlElement?)element.GetFirstChild<C.InvertIfNegative>() ?? element.GetFirstChild<C.Index>();
                if (before is not null) before.InsertAfterSelf(explosion);
                else element.PrependChild(explosion);
            }
            if (point.ColorHex is { } colour && Hex(colour) is { } hex)
            {
                element.RemoveAllChildren<C.ChartShapeProperties>();
                var properties = Lined(series)
                    ? new C.ChartShapeProperties(new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = hex })) { Width = 28575 })
                    : new C.ChartShapeProperties(new A.SolidFill(new A.RgbColorModelHex { Val = hex }));
                var anchor = element.ChildElements.LastOrDefault(child => child is C.Index or C.InvertIfNegative or C.Marker or C.Bubble3D or C.Explosion);
                if (anchor is not null) anchor.InsertAfterSelf(properties);
                else element.PrependChild(properties);
            }
        }
    }

    public ChartPointFormats Read(OpenXmlCompositeElement series)
    {
        var formats = new SortedDictionary<int, ChartPointFormat>();
        var scheme = _themes.For(series);
        foreach (var element in series.Elements<C.DataPoint>())
        {
            if (element.Index?.Val?.Value is not { } index || index > int.MaxValue) continue;
            var properties = element.GetFirstChild<C.ChartShapeProperties>();
            var fill = properties?.GetFirstChild<A.SolidFill>() ?? properties?.GetFirstChild<A.Outline>()?.GetFirstChild<A.SolidFill>();
            var colour = _fills.Hex(fill, scheme);
            if (colour is not null && series is C.PieChartSeries && _palette.IsAutomatic((int)index, colour)) colour = null;
            var explosion = element.GetFirstChild<C.Explosion>()?.Val?.Value is { } value ? (int)Math.Min(value, (uint)ChartPointFormats.MaximumExplosion) : 0;
            var point = new ChartPointFormat((int)index) { ColorHex = colour, ExplosionPercent = explosion };
            if (point.IsPlain) formats.Remove((int)index);
            else formats[(int)index] = point;
        }
        return formats.Count == 0 ? ChartPointFormats.None : new ChartPointFormats([.. formats.Values]);
    }

    private bool Lined(OpenXmlCompositeElement series) => series is C.LineChartSeries or C.ScatterChartSeries or C.RadarChartSeries;

    private string? Hex(string colour)
    {
        var hex = colour.TrimStart('#').ToUpperInvariant();
        return hex.Length == 6 && hex.All(char.IsAsciiHexDigit) ? hex : null;
    }

    private C.DataPoint? Find(OpenXmlCompositeElement series, int index) =>
        series.Elements<C.DataPoint>().FirstOrDefault(point => point.Index?.Val?.Value == (uint)index);

    private C.DataPoint Insert(OpenXmlCompositeElement series, int index)
    {
        var point = new C.DataPoint(new C.Index { Val = (uint)index });
        if (series is C.PieChartSeries or C.BubbleChartSeries) point.AppendChild(new C.Bubble3D { Val = false });
        var later = series.Elements<C.DataPoint>().FirstOrDefault(existing => existing.Index?.Val?.Value > (uint)index);
        if (later is not null)
        {
            later.InsertBeforeSelf(point);
            return point;
        }
        if (series.Elements<C.DataPoint>().LastOrDefault() is { } last)
        {
            last.InsertAfterSelf(point);
            return point;
        }
        var following = series.ChildElements.FirstOrDefault(child => child is C.DataLabels or C.Trendline or C.ErrorBars or C.CategoryAxisData or C.Values
            or C.XValues or C.YValues or C.BubbleSize or C.Smooth or C.Shape or C.BarSerExtensionList);
        if (following is not null) following.InsertBeforeSelf(point);
        else series.AppendChild(point);
        return point;
    }
}

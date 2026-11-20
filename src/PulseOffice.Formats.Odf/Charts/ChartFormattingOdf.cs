using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Charts;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfChartAxisScales : OdfChartXml
{
    private readonly OdfNumberStyleWriter _numbers = new();

    public XElement AxisStyle(string name, ChartAxisScale own, ChartAxisScale crossed, IList<XElement> numberStyles)
    {
        var properties = new XElement(Style + "chart-properties", new XAttribute(Chart + "display-label", "true"));
        if (own.Minimum is { } minimum && double.IsFinite(minimum)) properties.Add(new XAttribute(Chart + "minimum", Number(minimum)));
        if (own.Maximum is { } maximum && double.IsFinite(maximum)) properties.Add(new XAttribute(Chart + "maximum", Number(maximum)));
        if (own.MajorUnit is > 0 and var major && double.IsFinite(major)) properties.Add(new XAttribute(Chart + "interval-major", Number(major)));
        if (own.MinorUnit is > 0 and var minor && double.IsFinite(minor))
        {
            if (Divisor(own) is { } divisor) properties.Add(new XAttribute(Chart + "interval-minor-divisor", divisor.ToString(CultureInfo.InvariantCulture)));
            else properties.Add(new XAttribute(PulseChart + "interval-minor", Number(minor)));
        }
        if (own.Logarithmic)
        {
            properties.Add(new XAttribute(Chart + "logarithmic", "true"));
            if (Math.Abs(own.EffectiveLogBase - 10) > 1e-12) properties.Add(new XAttribute(PulseChart + "log-base", Number(own.EffectiveLogBase)));
        }
        if (own.Reversed) properties.Add(new XAttribute(Chart + "reverse-direction", "true"));
        switch (crossed.Crossing)
        {
            case AxisCrossing.Minimum: properties.Add(new XAttribute(Chart + "axis-position", "start")); break;
            case AxisCrossing.Maximum: properties.Add(new XAttribute(Chart + "axis-position", "end")); break;
            case AxisCrossing.Value when double.IsFinite(crossed.CrossingValue): properties.Add(new XAttribute(Chart + "axis-position", Number(crossed.CrossingValue))); break;
        }

        var style = new XElement(Style + "style",
            new XAttribute(Style + "name", name),
            new XAttribute(Style + "family", "chart"));
        if (own.NumberFormat is { Length: > 0 } code)
        {
            var dataStyle = name + "-number";
            foreach (var element in _numbers.Build(dataStyle, code)) numberStyles.Add(element);
            style.Add(new XAttribute(Style + "data-style-name", dataStyle));
            properties.Add(new XAttribute(Chart + "link-data-style-to-source", "false"));
        }
        style.Add(properties,
            new XElement(Style + "graphic-properties", new XAttribute(Svg + "stroke-color", Hairline)),
            new XElement(Style + "text-properties", new XAttribute(OdfPackage.Namespaces.Fo + "font-size", "10pt")));
        return style;
    }

    private int? Divisor(ChartAxisScale scale)
    {
        if (scale.MajorUnit is not > 0 || scale.MinorUnit is not > 0) return null;
        var ratio = scale.MajorUnit.Value / scale.MinorUnit.Value;
        var rounded = Math.Round(ratio);
        return rounded >= 1 && rounded <= 1000 && Math.Abs(ratio - rounded) < 1e-9 ? (int)rounded : null;
    }

    public ChartAxisScale Read(XElement? style, XElement? crossingStyle, OdfNumberStyleReader numbers)
    {
        var properties = style?.Element(Style + "chart-properties");
        if (properties is null && crossingStyle is null) return ChartAxisScale.Automatic;
        var major = Positive(properties, Chart + "interval-major");
        var divisor = (int?)Positive(properties, Chart + "interval-minor-divisor");
        var position = (string?)crossingStyle?.Element(Style + "chart-properties")?.Attribute(Chart + "axis-position");
        var crossingValue = Parse(position);
        var linked = (string?)properties?.Attribute(Chart + "link-data-style-to-source");
        var scale = new ChartAxisScale
        {
            Minimum = Finite(properties, Chart + "minimum"),
            Maximum = Finite(properties, Chart + "maximum"),
            MajorUnit = major,
            MinorUnit = Positive(properties, PulseChart + "interval-minor") ?? (major is { } unit && divisor is > 0 ? (double?)(unit / divisor.Value) : null),
            Logarithmic = (string?)properties?.Attribute(Chart + "logarithmic") == "true",
            LogBase = Positive(properties, PulseChart + "log-base") is > 1 and var logBase ? logBase : 10,
            Reversed = (string?)properties?.Attribute(Chart + "reverse-direction") == "true",
            Crossing = position switch
            {
                "start" => AxisCrossing.Minimum,
                "end" => AxisCrossing.Maximum,
                _ when crossingValue is not null => AxisCrossing.Value,
                _ => AxisCrossing.Automatic,
            },
            CrossingValue = crossingValue ?? 0,
            NumberFormat = linked == "false" && numbers.CodeOf((string?)style?.Attribute(Style + "data-style-name")) is { Length: > 0 } code && code != "General" ? code : null,
        };
        return scale.IsAutomatic ? ChartAxisScale.Automatic : scale;
    }

    private double? Finite(XElement? properties, XName name) => Parse((string?)properties?.Attribute(name));

    private double? Positive(XElement? properties, XName name) => Finite(properties, name) is > 0 and var value ? value : null;

    private double? Parse(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;

    private string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}

internal sealed class OdfChartSeriesFormats : OdfChartXml
{
    private readonly OdfLength _lengths = new();

    private readonly (TrendlineKind Kind, string Name)[] _regressions =
    [
        (TrendlineKind.Linear, "linear"),
        (TrendlineKind.Logarithmic, "logarithmic"),
        (TrendlineKind.Exponential, "exponential"),
        (TrendlineKind.Power, "power"),
        (TrendlineKind.Polynomial, "polynomial"),
        (TrendlineKind.MovingAverage, "moving-average"),
    ];

    private readonly (ErrorBarKind Kind, string Name)[] _errors =
    [
        (ErrorBarKind.FixedValue, "constant"),
        (ErrorBarKind.Percentage, "percentage"),
        (ErrorBarKind.StandardDeviation, "standard-deviation"),
        (ErrorBarKind.StandardError, "standard-error"),
        (ErrorBarKind.Custom, "cell-range"),
    ];

    private readonly SeriesSourceAddresses _addresses = new();

    private readonly OdfChartPointFormats _points = new();

    private readonly (MarkerSymbol Symbol, string Name)[] _symbols =
    [
        (MarkerSymbol.Square, "square"),
        (MarkerSymbol.Diamond, "diamond"),
        (MarkerSymbol.Triangle, "arrow-up"),
        (MarkerSymbol.Circle, "circle"),
        (MarkerSymbol.Star, "star"),
        (MarkerSymbol.Cross, "x"),
        (MarkerSymbol.Plus, "plus"),
        (MarkerSymbol.Dash, "horizontal-bar"),
        (MarkerSymbol.Dot, "circle"),
    ];

    private readonly (DataLabelPlacement Placement, string Name)[] _placements =
    [
        (DataLabelPlacement.Center, "center"),
        (DataLabelPlacement.InsideEnd, "inside"),
        (DataLabelPlacement.InsideBase, "near-origin"),
        (DataLabelPlacement.OutsideEnd, "outside"),
        (DataLabelPlacement.Above, "top"),
        (DataLabelPlacement.Below, "bottom"),
        (DataLabelPlacement.Left, "left"),
        (DataLabelPlacement.Right, "right"),
    ];

    private readonly OdfNumberStyleWriter _numbers = new();

    public IEnumerable<XAttribute> LabelProperties(bool shown, ChartDataLabelOptions options)
    {
        if (!shown)
        {
            yield return new XAttribute(Chart + "data-label-number", "none");
            yield break;
        }
        yield return new XAttribute(Chart + "data-label-number", (options.ShowValue, options.ShowPercent) switch
        {
            (true, true) => "value-and-percentage",
            (false, true) => "percentage",
            (true, false) => "value",
            _ => "none",
        });
        if (options.ShowCategory) yield return new XAttribute(Chart + "data-label-text", "true");
        if (options.ShowSeriesName) yield return new XAttribute(Chart + "data-label-series", "true");
        if (options.Placement != DataLabelPlacement.Automatic)
            yield return new XAttribute(Chart + "label-position", _placements.First(pair => pair.Placement == options.Placement).Name);
        if (options.NumberFormat is { Length: > 0 }) yield return new XAttribute(Chart + "link-data-style-to-source", "false");
    }

    public string? LabelNumberStyle(string seriesStyle, bool shown, ChartDataLabelOptions options, IList<XElement> numberStyles)
    {
        if (!shown || options.NumberFormat is not { Length: > 0 } code) return null;
        var name = seriesStyle + "-number";
        foreach (var element in _numbers.Build(name, code)) numberStyles.Add(element);
        return name;
    }

    public IEnumerable<XAttribute> MarkerProperties(ChartSeriesFormat format)
    {
        if (format.Marker == MarkerSymbol.Automatic) yield break;
        if (format.Marker == MarkerSymbol.None)
        {
            yield return new XAttribute(Chart + "symbol-type", "none");
            yield break;
        }
        yield return new XAttribute(Chart + "symbol-type", "named-symbol");
        yield return new XAttribute(Chart + "symbol-name", _symbols.First(pair => pair.Symbol == format.Marker).Name);
        if (format.Marker == MarkerSymbol.Dot) yield return new XAttribute(PulseChart + "marker", "dot");
        if (format.MarkerSize is { } size)
        {
            var length = Math.Clamp(size, 2, 72).ToString(CultureInfo.InvariantCulture) + "pt";
            yield return new XAttribute(Chart + "symbol-width", length);
            yield return new XAttribute(Chart + "symbol-height", length);
        }
    }

    public IEnumerable<XAttribute> LineProperties(ChartSeriesFormat format)
    {
        if (format.LineWidthPoints is > 0 and var width && double.IsFinite(width))
            yield return new XAttribute(Svg + "stroke-width", width.ToString("0.###", CultureInfo.InvariantCulture) + "pt");
        if (format.Dash != LineDash.Solid)
            yield return new XAttribute(PulseChart + "line-dash", format.Dash switch { LineDash.Dot => "dot", LineDash.DashDot => "dash-dot", _ => "dash" });
    }

    public IEnumerable<XElement> Children(string seriesStyle, ChartSeriesFormat format, IList<XElement> styles)
    {
        if (format.Trendline is { } trendline)
        {
            var name = seriesStyle + "-trend";
            styles.Add(new XElement(Style + "style", new XAttribute(Style + "name", name), new XAttribute(Style + "family", "chart"),
                new XElement(Style + "chart-properties", Regression(trendline))));
            yield return new XElement(Chart + "regression-curve", new XAttribute(Chart + "style-name", name),
                trendline.DisplayEquation || trendline.DisplayRSquared
                    ? new XElement(Chart + "equation",
                        new XAttribute(Chart + "display-equation", trendline.DisplayEquation ? "true" : "false"),
                        new XAttribute(Chart + "display-r-square", trendline.DisplayRSquared ? "true" : "false"))
                    : null);
        }
        if (format.ErrorBars is { } bars) yield return Indicator(seriesStyle + "-error", "y", bars, styles);
        if (format.XErrorBars is { } across) yield return Indicator(seriesStyle + "-error-x", "x", across, styles);
        foreach (var point in _points.Write(seriesStyle, format.Points, styles)) yield return point;
    }

    private XElement Indicator(string name, string dimension, ChartErrorBars bars, IList<XElement> styles)
    {
        styles.Add(new XElement(Style + "style", new XAttribute(Style + "name", name), new XAttribute(Style + "family", "chart"),
            new XElement(Style + "chart-properties", Errors(bars))));
        return new XElement(Chart + "error-indicator", new XAttribute(Chart + "style-name", name), new XAttribute(Chart + "dimension", dimension));
    }

    private IEnumerable<XAttribute> Regression(ChartTrendline trendline)
    {
        yield return new XAttribute(Chart + "regression-type", _regressions.First(pair => pair.Kind == trendline.Kind).Name);
        if (trendline.Kind == TrendlineKind.Polynomial) yield return new XAttribute(Chart + "regression-max-degree", trendline.EffectiveOrder.ToString(CultureInfo.InvariantCulture));
        if (trendline.Kind == TrendlineKind.MovingAverage) yield return new XAttribute(Chart + "regression-period", trendline.EffectivePeriod.ToString(CultureInfo.InvariantCulture));
        if (trendline.Forward > 0 && double.IsFinite(trendline.Forward)) yield return new XAttribute(Chart + "regression-extrapolate-forward", Number(trendline.Forward));
        if (trendline.Backward > 0 && double.IsFinite(trendline.Backward)) yield return new XAttribute(Chart + "regression-extrapolate-backward", Number(trendline.Backward));
        if (trendline.Intercept is { } intercept && double.IsFinite(intercept))
        {
            yield return new XAttribute(Chart + "regression-force-intercept", "true");
            yield return new XAttribute(Chart + "regression-intercept-value", Number(intercept));
        }
        if (trendline.Name is { Length: > 0 } name) yield return new XAttribute(Chart + "regression-name", name);
    }

    private IEnumerable<XAttribute> Errors(ChartErrorBars bars)
    {
        yield return new XAttribute(Chart + "error-category", _errors.First(pair => pair.Kind == bars.Kind).Name);
        yield return new XAttribute(Chart + "error-upper-indicator", bars.Direction != ErrorBarDirection.Minus ? "true" : "false");
        yield return new XAttribute(Chart + "error-lower-indicator", bars.Direction != ErrorBarDirection.Plus ? "true" : "false");
        var value = double.IsFinite(bars.Value) ? bars.Value : 1;
        switch (bars.Kind)
        {
            case ErrorBarKind.FixedValue:
                yield return new XAttribute(Chart + "error-upper-limit", Number(value));
                yield return new XAttribute(Chart + "error-lower-limit", Number(value));
                break;
            case ErrorBarKind.Percentage:
                yield return new XAttribute(Chart + "error-percentage", Number(value));
                break;
            case ErrorBarKind.StandardDeviation when Math.Abs(value - 1) > 1e-12:
                yield return new XAttribute(PulseChart + "error-value", Number(value));
                break;
            case ErrorBarKind.Custom:
                foreach (var attribute in Side("upper", bars.Plus)) yield return attribute;
                foreach (var attribute in Side("lower", bars.Minus)) yield return attribute;
                break;
        }
        if (!bars.EndCaps) yield return new XAttribute(PulseChart + "error-end-caps", "false");
    }

    private IEnumerable<XAttribute> Side(string side, ChartErrorValues? values)
    {
        if (_addresses.Address(values?.Reference) is { } address) yield return new XAttribute(Chart + $"error-{side}-range", address);
        if (values is { Values.Count: > 0 })
            yield return new XAttribute(PulseChart + $"error-{side}-values", string.Join(' ', values.Values.Select(value => double.IsFinite(value) ? Number(value) : "NaN")));
    }

    private ChartErrorValues ReadSide(XElement? properties, string side)
    {
        var reference = _addresses.Model(Attribute(properties, $"error-{side}-range"));
        var values = ((string?)properties?.Attribute(PulseChart + $"error-{side}-values"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(text => Parse(text) ?? double.NaN).ToArray() ?? [];
        return new ChartErrorValues(reference, values);
    }

    public ChartDataLabelOptions ReadLabels(XElement? properties, XElement? style, OdfNumberStyleReader numbers)
    {
        if (properties is null) return ChartDataLabelOptions.ValuesOnly;
        var number = (string?)properties.Attribute(Chart + "data-label-number") ?? "none";
        var position = (string?)properties.Attribute(Chart + "label-position");
        var options = new ChartDataLabelOptions
        {
            ShowValue = number is "value" or "value-and-percentage",
            ShowPercent = number is "percentage" or "value-and-percentage",
            ShowCategory = (string?)properties.Attribute(Chart + "data-label-text") == "true",
            ShowSeriesName = (string?)properties.Attribute(Chart + "data-label-series") == "true" || (string?)properties.Attribute(Loext + "data-label-series") == "true",
            Placement = position is null ? DataLabelPlacement.Automatic
                : _placements.Where(pair => pair.Name == position).Select(pair => pair.Placement).DefaultIfEmpty(DataLabelPlacement.Automatic).First(),
            NumberFormat = (string?)properties.Attribute(Chart + "link-data-style-to-source") == "false"
                && numbers.CodeOf((string?)style?.Attribute(Style + "data-style-name")) is { Length: > 0 } code && code != "General" ? code : null,
        };
        return options.IsValuesOnly ? ChartDataLabelOptions.ValuesOnly : options;
    }

    public bool LabelsShown(XElement? properties) =>
        properties is not null
        && (((string?)properties.Attribute(Chart + "data-label-number") ?? "none") != "none"
            || (string?)properties.Attribute(Chart + "data-label-text") == "true"
            || (string?)properties.Attribute(Chart + "data-label-series") == "true"
            || (string?)properties.Attribute(Loext + "data-label-series") == "true");

    public ChartSeriesFormat Read(XElement series, XElement? style, Func<string?, XElement?> named)
    {
        var properties = style?.Element(Style + "chart-properties");
        var graphic = style?.Element(Style + "graphic-properties");
        var symbolType = (string?)properties?.Attribute(Chart + "symbol-type");
        var symbolName = (string?)properties?.Attribute(Chart + "symbol-name");
        var dash = (string?)graphic?.Attribute(PulseChart + "line-dash");
        var format = new ChartSeriesFormat
        {
            LineWidthPoints = _lengths.Parse((string?)graphic?.Attribute(Svg + "stroke-width")) is { } width && width.Value > 0
                ? width.ToPoints() : null,
            Dash = dash switch { "dot" => LineDash.Dot, "dash-dot" => LineDash.DashDot, "dash" => LineDash.Dash, _ => LineDash.Solid },
            Marker = symbolType switch
            {
                "none" => MarkerSymbol.None,
                "named-symbol" when (string?)properties?.Attribute(PulseChart + "marker") == "dot" => MarkerSymbol.Dot,
                "named-symbol" => _symbols.Where(pair => pair.Name == symbolName).Select(pair => pair.Symbol)
                    .DefaultIfEmpty(symbolName == "asterisk" ? MarkerSymbol.Star : symbolName == "vertical-bar" ? MarkerSymbol.Dash : MarkerSymbol.Square).First(),
                _ => MarkerSymbol.Automatic,
            },
            MarkerSize = symbolType == "named-symbol" && _lengths.Parse((string?)properties?.Attribute(Chart + "symbol-width")) is { } size && size.Value > 0
                ? (int)Math.Round(size.ToPoints()) : null,
            SecondaryAxis = (string?)series.Attribute(Chart + "attached-axis") is { } axis && axis.StartsWith("secondary", StringComparison.Ordinal),
            Trendline = series.Element(Chart + "regression-curve") is { } curve ? Trendline(curve, named((string?)curve.Attribute(Chart + "style-name"))?.Element(Style + "chart-properties")) : null,
            ErrorBars = series.Elements(Chart + "error-indicator").FirstOrDefault(indicator => (string?)indicator.Attribute(Chart + "dimension") is null or "y") is { } error
                ? Errors(named((string?)error.Attribute(Chart + "style-name"))?.Element(Style + "chart-properties") ?? properties)
                : properties?.Attribute(Chart + "error-category") is { } category && category.Value != "none" ? Errors(properties) : null,
            XErrorBars = series.Elements(Chart + "error-indicator").FirstOrDefault(indicator => (string?)indicator.Attribute(Chart + "dimension") == "x") is { } across
                ? Errors(named((string?)across.Attribute(Chart + "style-name"))?.Element(Style + "chart-properties"))
                : null,
            Points = _points.Read(series, named),
        };
        return format.IsPlain ? ChartSeriesFormat.Plain : format;
    }

    private ChartTrendline? Trendline(XElement curve, XElement? properties)
    {
        var type = Attribute(properties, "regression-type");
        if (type is null or "none") return null;
        var kind = _regressions.Where(pair => pair.Name == type).Select(pair => (TrendlineKind?)pair.Kind).FirstOrDefault();
        if (kind is null) return null;
        var equation = curve.Element(Chart + "equation");
        return new ChartTrendline(kind.Value)
        {
            Order = (int?)Parse(Attribute(properties, "regression-max-degree")) ?? 2,
            Period = (int?)Parse(Attribute(properties, "regression-period")) ?? 2,
            Forward = Parse(Attribute(properties, "regression-extrapolate-forward")) is > 0 and var forward ? forward : 0,
            Backward = Parse(Attribute(properties, "regression-extrapolate-backward")) is > 0 and var backward ? backward : 0,
            Intercept = Attribute(properties, "regression-force-intercept") == "true" ? Parse(Attribute(properties, "regression-intercept-value")) ?? 0 : null,
            Name = Attribute(properties, "regression-name") is { Length: > 0 } name ? name : null,
            DisplayEquation = (string?)equation?.Attribute(Chart + "display-equation") == "true",
            DisplayRSquared = (string?)equation?.Attribute(Chart + "display-r-square") == "true",
        };
    }

    private ChartErrorBars? Errors(XElement? properties)
    {
        var category = Attribute(properties, "error-category");
        var kind = _errors.Where(pair => pair.Name == category).Select(pair => (ErrorBarKind?)pair.Kind).FirstOrDefault();
        if (kind is null) return null;
        var upper = Attribute(properties, "error-upper-indicator") != "false";
        var lower = Attribute(properties, "error-lower-indicator") != "false";
        var value = kind switch
        {
            ErrorBarKind.FixedValue => Parse(Attribute(properties, "error-upper-limit")) ?? Parse(Attribute(properties, "error-lower-limit")) ?? 1,
            ErrorBarKind.Percentage => Parse(Attribute(properties, "error-percentage")) ?? 1,
            ErrorBarKind.StandardDeviation => Parse((string?)properties?.Attribute(PulseChart + "error-value")) ?? 1,
            _ => 1,
        };
        return new ChartErrorBars(kind.Value, value)
        {
            Direction = upper && !lower ? ErrorBarDirection.Plus : lower && !upper ? ErrorBarDirection.Minus : ErrorBarDirection.Both,
            EndCaps = (string?)properties?.Attribute(PulseChart + "error-end-caps") != "false",
            Plus = kind == ErrorBarKind.Custom ? ReadSide(properties, "upper") : null,
            Minus = kind == ErrorBarKind.Custom ? ReadSide(properties, "lower") : null,
        };
    }

    private string? Attribute(XElement? properties, string name) =>
        (string?)properties?.Attribute(Chart + name) ?? (string?)properties?.Attribute(Loext + name);

    private double? Parse(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;

    private string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}

internal sealed class OdfChartPointFormats : OdfChartXml
{
    private const int MostPoints = 100_000;

    public IEnumerable<XElement> Write(string seriesStyle, ChartPointFormats points, IList<XElement> styles)
    {
        var next = 0;
        foreach (var point in points.Items.Where(item => item.Index >= 0 && item.Index < MostPoints))
        {
            if (point.Index > next) yield return Repeated(point.Index - next);
            var name = $"{seriesStyle}-point-{point.Index + 1}";
            var graphic = new XElement(Style + "graphic-properties");
            if (point.ColorHex is { Length: 7 } colour && colour[0] == '#')
                graphic.Add(new XAttribute(Draw + "fill", "solid"), new XAttribute(Draw + "fill-color", colour.ToLowerInvariant()),
                    new XAttribute(Svg + "stroke-color", colour.ToLowerInvariant()));
            var properties = new XElement(Style + "chart-properties");
            if (point.ExplosionPercent > 0)
                properties.Add(new XAttribute(Chart + "pie-offset", point.ExplosionPercent.ToString(CultureInfo.InvariantCulture)));
            styles.Add(new XElement(Style + "style", new XAttribute(Style + "name", name), new XAttribute(Style + "family", "chart"),
                properties.HasAttributes ? properties : null, graphic.HasAttributes ? graphic : null));
            yield return new XElement(Chart + "data-point", new XAttribute(Chart + "style-name", name));
            next = point.Index + 1;
        }
    }

    public ChartPointFormats Read(XElement series, Func<string?, XElement?> named)
    {
        var formats = new List<ChartPointFormat>();
        var index = 0;
        foreach (var element in series.Elements(Chart + "data-point"))
        {
            if (index >= MostPoints) break;
            var repeated = int.TryParse((string?)element.Attribute(Chart + "repeated"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                ? Math.Clamp(count, 1, MostPoints) : 1;
            var style = named((string?)element.Attribute(Chart + "style-name"));
            var graphic = style?.Element(Style + "graphic-properties");
            var fill = (string?)graphic?.Attribute(Draw + "fill") == "none" ? null
                : (string?)graphic?.Attribute(Draw + "fill-color") ?? (string?)graphic?.Attribute(Svg + "stroke-color");
            var colour = fill is { Length: 7 } && fill[0] == '#' && fill.Skip(1).All(char.IsAsciiHexDigit) ? fill.ToUpperInvariant() : null;
            var explosion = int.TryParse((string?)style?.Element(Style + "chart-properties")?.Attribute(Chart + "pie-offset"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset)
                ? offset : 0;
            if (colour is not null || explosion > 0)
                for (var copy = 0; copy < repeated && index + copy < MostPoints; copy++)
                    formats.Add(new ChartPointFormat(index + copy) { ColorHex = colour, ExplosionPercent = Math.Min(explosion, ChartPointFormats.MaximumExplosion) });
            index += repeated;
        }
        return formats.Count == 0 ? ChartPointFormats.None : new ChartPointFormats(formats);
    }

    private XElement Repeated(int count) =>
        count == 1 ? new XElement(Chart + "data-point") : new XElement(Chart + "data-point", new XAttribute(Chart + "repeated", count.ToString(CultureInfo.InvariantCulture)));
}

internal sealed class OdfChartExtensions
{
    public const string Loss = "Charts use dashed series lines, a logarithm base other than 10, error bars without end caps, a standard deviation multiplier or error values not taken from cells: ODF has no attribute for these, so they are kept in a PulseOffice extension and other applications show solid lines, base 10, capped bars, one standard deviation and no custom error bars. The OOXML formats keep them.";

    public bool Needs(ChartDefinition chart) =>
        new[] { chart.CategoryAxis, chart.ValueAxis, chart.SecondaryValueAxis }.Any(scale => scale.Logarithmic && Math.Abs(scale.EffectiveLogBase - 10) > 1e-12)
        || chart.SeriesFormats?.Any(format => format is not null
            && (format.Dash != LineDash.Solid || format.Marker == MarkerSymbol.Dot
                || Extended(format.ErrorBars) || Extended(format.XErrorBars))) == true;

    private bool Extended(ChartErrorBars? bars) =>
        bars is not null
        && (!bars.EndCaps
            || bars.Kind == ErrorBarKind.StandardDeviation && Math.Abs(bars.Value - 1) > 1e-12
            || bars.Kind == ErrorBarKind.Custom && (bars.Plus?.Reference is null || bars.Minus?.Reference is null));
}

using PulseOffice.Foundation.Colors;
using PulseOffice.Formats;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

public sealed class OdfSparklineMapper
{
    private readonly TokenMap<SparklineKind> _kinds = new(SparklineKind.Line, "line",
        (SparklineKind.Column, "column"), (SparklineKind.Stacked, "stacked"));

    private readonly TokenMap<SparklineEmptyCells> _empty = new(SparklineEmptyCells.Gap, "gap",
        (SparklineEmptyCells.Zero, "zero"), (SparklineEmptyCells.Span, "span"));

    private readonly TokenMap<SparklineAxisBound> _bounds = new(SparklineAxisBound.Individual, "individual",
        (SparklineAxisBound.Group, "group"), (SparklineAxisBound.Custom, "custom"));

    private readonly XName _dateRange = XNamespace.Get("urn:pulseoffice:spreadsheet") + "sparkline-date-range";

    private readonly OdfQualifiedRanges _ranges = new();

    public XElement? Build(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (sheet.Sparklines.Count == 0) return null;

        var container = new XElement(Namespaces.Calcext + "sparkline-groups");

        foreach (var group in sheet.Sparklines.Groups)
        {
            var style = group.Style;
            var element = new XElement(Namespaces.Calcext + "sparkline-group",
                new XAttribute(Namespaces.Calcext + "id", GroupGuid(group.Id).ToString("B").ToUpperInvariant()),
                new XAttribute(Namespaces.Calcext + "type", _kinds.Token(group.Kind)),
                new XAttribute(Namespaces.Calcext + "line-width",
                    style.LineWidth.ToString("0.##", CultureInfo.InvariantCulture) + "pt"),
                new XAttribute(Namespaces.Calcext + "display-empty-cells-as", _empty.Token(group.EmptyCells)),
                Flag("date-axis", group.DateRange is not null),
                Flag("markers", style.ShowMarkers),
                Flag("high", style.ShowHighPoint),
                Flag("low", style.ShowLowPoint),
                Flag("first", style.ShowFirstPoint),
                Flag("last", style.ShowLastPoint),
                Flag("negative", style.ShowNegative),
                Flag("display-x-axis", style.ShowAxis),
                Flag("display-hidden", group.ShowHidden),
                Flag("right-to-left", group.RightToLeft),
                new XAttribute(Namespaces.Calcext + "min-axis-type", _bounds.Token(group.MinimumAxis)),
                new XAttribute(Namespaces.Calcext + "max-axis-type", _bounds.Token(group.MaximumAxis)),
                new XAttribute(Namespaces.Calcext + "color-series", style.ColorHex),
                new XAttribute(Namespaces.Calcext + "color-negative", style.NegativeHex),
                new XAttribute(Namespaces.Calcext + "color-axis", style.AxisHex),
                new XAttribute(Namespaces.Calcext + "color-markers", style.MarkerHex),
                new XAttribute(Namespaces.Calcext + "color-first", style.FirstHex ?? style.MarkerHex),
                new XAttribute(Namespaces.Calcext + "color-last", style.LastHex ?? style.MarkerHex),
                new XAttribute(Namespaces.Calcext + "color-high", style.HighHex ?? style.MarkerHex),
                new XAttribute(Namespaces.Calcext + "color-low", style.LowHex ?? style.MarkerHex));
            if (group.MinimumAxis == SparklineAxisBound.Custom && group.ManualMinimum is { } minimum)
                element.Add(new XAttribute(Namespaces.Calcext + "manual-min", minimum.ToString("R", CultureInfo.InvariantCulture)));
            if (group.MaximumAxis == SparklineAxisBound.Custom && group.ManualMaximum is { } maximum)
                element.Add(new XAttribute(Namespaces.Calcext + "manual-max", maximum.ToString("R", CultureInfo.InvariantCulture)));
            if (group.DateRange is { } dates)
                element.Add(new XAttribute(_dateRange, _ranges.Write(group.DateSheet ?? sheet.Name, dates)));

            var lines = new XElement(Namespaces.Calcext + "sparklines");
            foreach (var sparkline in group.Sparklines)
                lines.Add(new XElement(Namespaces.Calcext + "sparkline",
                    new XAttribute(Namespaces.Calcext + "cell-address", _ranges.WriteAddress(sheet.Name, sparkline.Cell)),
                    new XAttribute(Namespaces.Calcext + "data-range", _ranges.Write(sparkline.SourceSheetOr(sheet.Name), sparkline.Source))));

            element.Add(lines);
            container.Add(element);
        }

        return container;
    }

    public void Read(XElement table, Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(sheet);

        var container = table.Element(Namespaces.Calcext + "sparkline-groups");
        if (container is null) return;

        foreach (var element in container.Elements(Namespaces.Calcext + "sparkline-group"))
        {
            var lines = new List<Sparkline>();
            var holders = element.Elements(Namespaces.Calcext + "sparklines").Elements(Namespaces.Calcext + "sparkline")
                .Concat(element.Elements(Namespaces.Calcext + "sparkline"));

            foreach (var sparkline in holders)
            {
                var cell = _ranges.Read(sparkline.Attribute(Namespaces.Calcext + "cell-address")?.Value);
                var source = _ranges.Read(
                    sparkline.Attribute(Namespaces.Calcext + "data-range")?.Value
                    ?? sparkline.Attribute(Namespaces.Calcext + "cell-range-address")?.Value);

                if (cell is null || source is null) continue;
                lines.Add(new Sparkline(cell.Value.Range.Start, source.Value.Range, Foreign(source.Value.Sheet, sheet)));
            }

            if (lines.Count == 0) continue;

            var defaults = new SparklineStyle();
            var marker = Color(Text(element, "color-markers"));
            var sameScale = Text(element, "same-scale") == "true";
            var minimum = sameScale ? SparklineAxisBound.Group : _bounds.Value(Text(element, "min-axis-type"));
            var maximum = sameScale ? SparklineAxisBound.Group : _bounds.Value(Text(element, "max-axis-type"));
            var dates = _ranges.Read(element.Attribute(_dateRange)?.Value);
            sheet.Sparklines.Add(new SparklineGroup
            {
                Id = element.Attribute(Namespaces.Calcext + "id")?.Value.Trim('{', '}') ?? Guid.NewGuid().ToString("N"),
                Sparklines = lines,
                Kind = _kinds.Value(Text(element, "type")),
                MinimumAxis = minimum,
                MaximumAxis = maximum,
                ManualMinimum = minimum == SparklineAxisBound.Custom ? Number(Text(element, "manual-min")) : null,
                ManualMaximum = maximum == SparklineAxisBound.Custom ? Number(Text(element, "manual-max")) : null,
                EmptyCells = _empty.Value(Text(element, "display-empty-cells-as")),
                ShowHidden = Text(element, "display-hidden") == "true",
                RightToLeft = Text(element, "right-to-left") == "true",
                DateRange = dates?.Range,
                DateSheet = dates is { } date ? Foreign(date.Sheet, sheet) : null,
                Style = new SparklineStyle
                {
                    ColorHex = Color(Text(element, "color-series") ?? Text(element, "color")) ?? defaults.ColorHex,
                    NegativeHex = Color(Text(element, "color-negative") ?? Text(element, "negative-color")) ?? defaults.NegativeHex,
                    MarkerHex = marker ?? defaults.MarkerHex,
                    AxisHex = Color(Text(element, "color-axis")) ?? defaults.AxisHex,
                    FirstHex = Distinct(Color(Text(element, "color-first")), marker),
                    LastHex = Distinct(Color(Text(element, "color-last")), marker),
                    HighHex = Distinct(Color(Text(element, "color-high")), marker),
                    LowHex = Distinct(Color(Text(element, "color-low")), marker),
                    ShowMarkers = Text(element, "markers") == "true",
                    ShowAxis = Text(element, "display-x-axis") == "true",
                    ShowHighPoint = Text(element, "high") == "true",
                    ShowLowPoint = Text(element, "low") == "true",
                    ShowFirstPoint = Text(element, "first") == "true",
                    ShowLastPoint = Text(element, "last") == "true",
                    ShowNegative = Text(element, "negative") is { } negative ? negative == "true" : Text(element, "color") is not null,
                    LineWidth = Width(Text(element, "line-width")) ?? defaults.LineWidth,
                },
            });
        }
    }

    private string? Foreign(string source, Worksheet sheet) =>
        source.Length > 0 && !string.Equals(source, sheet.Name, StringComparison.OrdinalIgnoreCase) ? source : null;

    private string? Distinct(string? colour, string? marker) =>
        colour is not null && !string.Equals(colour, marker, StringComparison.OrdinalIgnoreCase) ? colour : null;

    private double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;

    private XAttribute Flag(string name, bool value) => new(Namespaces.Calcext + name, value ? "true" : "false");

    private string? Text(XElement element, string name) => element.Attribute(Namespaces.Calcext + name)?.Value;

    private double? Width(string? text)
    {
        if (text is null) return null;
        var number = text.EndsWith("pt", StringComparison.OrdinalIgnoreCase) ? text[..^2] : text;
        return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) && double.IsFinite(width) && width > 0
            ? Math.Clamp(width, SparklineStyle.ThinnestLine, SparklineStyle.WidestLine)
            : null;
    }

    private string? Color(string? text) => ArgbColor.FromHashedRgbHex(text) is not null ? text : null;

    private Guid GroupGuid(string id) =>
        Guid.TryParse(id, out var guid) ? guid : new Guid(MD5.HashData(Encoding.UTF8.GetBytes(id)));
}

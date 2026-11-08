using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Formats.OpenXml;

internal sealed class X14Thresholds(X14Namespaces names, XlsxFormulaSyntax formulas, TokenMap<ConditionalThresholdKind> kinds)
{
    public XElement Write(ConditionalThreshold threshold)
    {
        var entry = new XElement(names.X14 + "cfvo", new XAttribute("type", kinds.Token(threshold.Kind)));
        if (!threshold.GreaterOrEqual) entry.Add(new XAttribute("gte", "0"));
        if (threshold.Kind is not (ConditionalThresholdKind.Minimum or ConditionalThresholdKind.Maximum
            or ConditionalThresholdKind.AutomaticMinimum or ConditionalThresholdKind.AutomaticMaximum))
            entry.Add(new XElement(names.Xm + "f", threshold.Kind == ConditionalThresholdKind.Formula && threshold.Formula is { } formula
                ? formulas.ToOpenXml(formula)
                : (double.IsFinite(threshold.Value) ? threshold.Value : 0).ToString("R", CultureInfo.InvariantCulture)));
        return entry;
    }

    public ConditionalThreshold Read(XElement entry)
    {
        var kind = kinds.Value((string?)entry.Attribute("type") ?? "num");
        var greater = !names.IsFalse(entry.Attribute("gte"));
        if (kind is ConditionalThresholdKind.Minimum or ConditionalThresholdKind.Maximum
            or ConditionalThresholdKind.AutomaticMinimum or ConditionalThresholdKind.AutomaticMaximum)
            return new ConditionalThreshold(kind, 0, null, greater);

        var text = (string?)entry.Element(names.Xm + "f") ?? (string?)entry.Attribute("val") ?? (string?)entry.Attribute("value") ?? "0";
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return new ConditionalThreshold(kind == ConditionalThresholdKind.Formula ? ConditionalThresholdKind.Number : kind, double.IsFinite(number) ? number : 0, null, greater);
        return new ConditionalThreshold(ConditionalThresholdKind.Formula, 0, formulas.ToInternal(text).TrimStart('='), greater);
    }
}

internal sealed class X14RuleIds
{
    private readonly Dictionary<ConditionalRule, string> _ids = new(ReferenceEqualityComparer.Instance);

    public string For(ConditionalRule rule)
    {
        if (_ids.TryGetValue(rule, out var existing)) return existing;

        var id = "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}";
        _ids[rule] = id;
        return id;
    }
}

internal sealed record X14DataBarEntry(XElement Rule, IReadOnlyList<CellRange> Ranges);

internal sealed class X14DataBarLinks(IReadOnlyDictionary<string, X14DataBarEntry> entries)
{
    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);

    public X14DataBarEntry? Take(string? id)
    {
        if (id is null || !entries.TryGetValue(id, out var entry)) return null;

        _used.Add(id);
        return entry;
    }

    public IEnumerable<X14DataBarEntry> Unused => entries.Where(pair => !_used.Contains(pair.Key)).Select(pair => pair.Value);
}

internal sealed class X14DataBarCodec(X14Namespaces names, X14Thresholds thresholds, SpreadsheetColours colours)
{
    public const string RuleExtensionUri = "{B025F937-C7B1-47D3-B67F-A62EFF666E3E}";

    private readonly TokenMap<DataBarAxis> _axes = new(
        DataBarAxis.Automatic, "automatic",
        (DataBarAxis.Automatic, "automatic"),
        (DataBarAxis.Middle, "middle"),
        (DataBarAxis.None, "none"));

    private readonly TokenMap<DataBarDirection> _directions = new(
        DataBarDirection.Context, "context",
        (DataBarDirection.Context, "context"),
        (DataBarDirection.LeftToRight, "leftToRight"),
        (DataBarDirection.RightToLeft, "rightToLeft"));

    private XNamespace X14 => names.X14;

    public XElement Write(ConditionalRule rule, string id)
    {
        var options = rule.Bar;
        var bar = new XElement(X14 + "dataBar",
            new XAttribute("minLength", Length(options.MinimumLength)),
            new XAttribute("maxLength", Length(options.MaximumLength)));
        if (!rule.ShowValue) bar.Add(new XAttribute("showValue", "0"));
        if (options.BorderColorHex is not null) bar.Add(new XAttribute("border", "1"));
        if (!options.Gradient) bar.Add(new XAttribute("gradient", "0"));
        if (options.Direction != DataBarDirection.Context) bar.Add(new XAttribute("direction", _directions.Token(options.Direction)));
        if (rule.NegativeBarColorHex is null) bar.Add(new XAttribute("negativeBarColorSameAsPositive", "1"));
        if (options.NegativeBorderColorHex is not null) bar.Add(new XAttribute("negativeBarBorderColorSameAsPositive", "0"));
        if (options.Axis != DataBarAxis.Automatic) bar.Add(new XAttribute("axisPosition", _axes.Token(options.Axis)));

        IEnumerable<ConditionalThreshold> stops = rule.Thresholds.Count >= 2
            ? rule.Thresholds.Take(2)
            : [new ConditionalThreshold(ConditionalThresholdKind.AutomaticMinimum), new ConditionalThreshold(ConditionalThresholdKind.AutomaticMaximum)];
        bar.Add(stops.Select(thresholds.Write));
        bar.Add(Colour("fillColor", rule.BarColorHex));
        if (options.BorderColorHex is { } border) bar.Add(Colour("borderColor", border));
        if (rule.NegativeBarColorHex is { } negative) bar.Add(Colour("negativeFillColor", negative));
        if (options.NegativeBorderColorHex is { } negativeBorder) bar.Add(Colour("negativeBorderColor", negativeBorder));
        bar.Add(Colour("axisColor", options.AxisColorHex));

        return new XElement(X14 + "conditionalFormatting",
            new XElement(X14 + "cfRule", new XAttribute("type", "dataBar"), new XAttribute("id", id), bar),
            new XElement(names.Xm + "sqref", rule.RangeText));
    }

    public ConditionalRule Apply(ConditionalRule rule, XElement cfRule)
    {
        if (cfRule.Element(X14 + "dataBar") is not { } bar) return rule;

        var stops = bar.Elements(X14 + "cfvo").Select(thresholds.Read).ToList();
        var sameNegative = names.IsTrue(bar.Attribute("negativeBarColorSameAsPositive"));
        var sameNegativeBorder = !names.IsFalse(bar.Attribute("negativeBarBorderColorSameAsPositive"));
        var bordered = names.IsTrue(bar.Attribute("border"));

        return rule with
        {
            Visual = ConditionalVisual.DataBar,
            BarColorHex = Colour(bar.Element(X14 + "fillColor")) ?? rule.BarColorHex,
            NegativeBarColorHex = sameNegative ? null : Colour(bar.Element(X14 + "negativeFillColor")) ?? "#FF0000",
            ShowValue = !names.IsFalse(bar.Attribute("showValue")) && rule.ShowValue,
            Thresholds = stops is [{ Kind: ConditionalThresholdKind.AutomaticMinimum }, { Kind: ConditionalThresholdKind.AutomaticMaximum }]
                ? []
                : stops.Count >= 2 ? stops : rule.Thresholds,
            Bar = new DataBarOptions
            {
                Gradient = !names.IsFalse(bar.Attribute("gradient")),
                Axis = _axes.Value((string?)bar.Attribute("axisPosition")),
                AxisColorHex = Colour(bar.Element(X14 + "axisColor")) ?? "#000000",
                BorderColorHex = bordered ? Colour(bar.Element(X14 + "borderColor")) ?? "#000000" : null,
                NegativeBorderColorHex = bordered && !sameNegativeBorder ? Colour(bar.Element(X14 + "negativeBorderColor")) ?? "#FF0000" : null,
                Direction = _directions.Value((string?)bar.Attribute("direction")),
                MinimumLength = Length(bar.Attribute("minLength"), 10),
                MaximumLength = Length(bar.Attribute("maxLength"), 90),
            },
        };
    }

    public ConditionalRule Read(X14DataBarEntry entry) => Apply(new ConditionalRule
    {
        Range = entry.Ranges[0],
        AdditionalRanges = [.. entry.Ranges.Skip(1)],
        Priority = (int.TryParse((string?)entry.Rule.Attribute("priority"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority) && priority > 0 ? priority : 1) - 1,
        Visual = ConditionalVisual.DataBar,
    }, entry.Rule);

    private XElement Colour(string name, string hex) => new(X14 + name, new XAttribute("rgb", colours.ToArgb(hex)));

    private string? Colour(XElement? element) => colours.Parse((string?)element?.Attribute("rgb"));

    private string Length(double value) => Math.Round(Math.Clamp(value, 0, 100)).ToString(CultureInfo.InvariantCulture);

    private double Length(XAttribute? attribute, double fallback) =>
        double.TryParse((string?)attribute, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= 100 ? value : fallback;
}

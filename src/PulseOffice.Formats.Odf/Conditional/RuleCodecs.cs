using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal interface IConditionStyles
{
    string NameFor(CellStyle style);
}

internal sealed record RuleReadContext(CellRange Range, int Priority, IReadOnlyDictionary<string, CellStyle> NamedStyles);

internal abstract class ConditionalRuleCodec
{
    public abstract XName Element { get; }

    public abstract bool Writes(ConditionalRule rule);

    public abstract XElement Write(Worksheet sheet, ConditionalRule rule);

    public abstract ConditionalRule Read(XElement element, RuleReadContext context);

    protected XName Calc(string name) => Namespaces.Calcext + name;

    protected string? Upper(string? hex) => hex?.ToUpperInvariant();
}

internal sealed class OdfThresholds
{
    private readonly TokenMap<ConditionalThresholdKind> _kinds = new(
        ConditionalThresholdKind.Number,
        "number",
        (ConditionalThresholdKind.Minimum, "minimum"),
        (ConditionalThresholdKind.Maximum, "maximum"),
        (ConditionalThresholdKind.Number, "number"),
        (ConditionalThresholdKind.Percent, "percent"),
        (ConditionalThresholdKind.Percentile, "percentile"),
        (ConditionalThresholdKind.Formula, "formula"),
        (ConditionalThresholdKind.AutomaticMinimum, "auto-minimum"),
        (ConditionalThresholdKind.AutomaticMaximum, "auto-maximum"));

    private readonly OdfFormulaSyntax _formulas = new();

    public void Write(XElement entry, ConditionalThreshold threshold)
    {
        entry.SetAttributeValue(Namespaces.Calcext + "type", _kinds.Token(threshold.Kind));
        entry.SetAttributeValue(Namespaces.Calcext + "value", threshold.Kind == ConditionalThresholdKind.Formula && threshold.Formula is { } formula
            ? _formulas.ToOdfExpression("=" + formula.TrimStart('='))
            : threshold.Value.ToString("R", CultureInfo.InvariantCulture));
        entry.SetAttributeValue(Namespaces.Calcext + "greater_equal", threshold.GreaterOrEqual ? null : "false");
    }

    public ConditionalThreshold Read(XElement entry) =>
        Kind(entry) with
        {
            GreaterOrEqual = ((string?)entry.Attribute(Namespaces.Calcext + "greater_equal") ?? (string?)entry.Attribute(Namespaces.Calcext + "greater-equal")) is not ("false" or "0"),
        };

    private ConditionalThreshold Kind(XElement entry)
    {
        var type = (string?)entry.Attribute(Namespaces.Calcext + "type") ?? "number";
        var text = (string?)entry.Attribute(Namespaces.Calcext + "value") ?? "0";
        var kind = _kinds.Value(type);
        if (kind == ConditionalThresholdKind.Formula)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var constant) && double.IsFinite(constant))
                return new ConditionalThreshold(ConditionalThresholdKind.Number, constant);
            return new ConditionalThreshold(kind, 0, _formulas.ToInternalExpression(text));
        }
        return new ConditionalThreshold(kind, double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : 0);
    }
}

internal sealed class DataBarCodec(OdfThresholds thresholds) : ConditionalRuleCodec
{
    private readonly TokenMap<DataBarAxis> _axes = new(
        DataBarAxis.Automatic, "automatic",
        (DataBarAxis.Automatic, "automatic"),
        (DataBarAxis.Middle, "middle"),
        (DataBarAxis.None, "none"));

    public override XName Element => Calc("data-bar");

    public override bool Writes(ConditionalRule rule) => rule.Visual == ConditionalVisual.DataBar;

    public override XElement Write(Worksheet sheet, ConditionalRule rule)
    {
        var options = rule.Bar;
        var bar = new XElement(Element,
            new XAttribute(Calc("positive-color"), rule.BarColorHex.ToLowerInvariant()),
            new XAttribute(Calc("axis-position"), _axes.Token(options.Axis)),
            new XAttribute(Calc("axis-color"), options.AxisColorHex.ToLowerInvariant()));
        if (rule.NegativeBarColorHex is { } negative) bar.Add(new XAttribute(Calc("negative-color"), negative.ToLowerInvariant()));
        if (!options.Gradient) bar.Add(new XAttribute(Calc("gradient"), "false"));
        if (!rule.ShowValue) bar.Add(new XAttribute(Calc("show-value"), "false"));
        if (options.MinimumLength != 0) bar.Add(new XAttribute(Calc("min-length"), options.MinimumLength.ToString("R", CultureInfo.InvariantCulture)));
        bar.Add(new XAttribute(Calc("max-length"), options.MaximumLength.ToString("R", CultureInfo.InvariantCulture)));

        var entries = rule.Thresholds.Count >= 2
            ? rule.Thresholds.Take(2)
            : [new ConditionalThreshold(ConditionalThresholdKind.AutomaticMinimum), new ConditionalThreshold(ConditionalThresholdKind.AutomaticMaximum)];
        foreach (var threshold in entries)
        {
            var entry = new XElement(Calc("formatting-entry"));
            thresholds.Write(entry, threshold);
            bar.Add(entry);
        }
        return bar;
    }

    public override ConditionalRule Read(XElement element, RuleReadContext context)
    {
        var entries = element.Elements(Calc("formatting-entry")).Select(thresholds.Read).ToList();
        var automatic = entries is [{ Kind: ConditionalThresholdKind.AutomaticMinimum }, { Kind: ConditionalThresholdKind.AutomaticMaximum }];
        return new ConditionalRule
        {
            Range = context.Range,
            Priority = context.Priority,
            Visual = ConditionalVisual.DataBar,
            BarColorHex = Upper((string?)element.Attribute(Calc("positive-color"))) ?? "#638EC6",
            NegativeBarColorHex = Upper((string?)element.Attribute(Calc("negative-color"))),
            ShowValue = (string?)element.Attribute(Calc("show-value")) is not ("false" or "0"),
            Thresholds = automatic ? [] : entries,
            Bar = new DataBarOptions
            {
                Gradient = (string?)element.Attribute(Calc("gradient")) is not ("false" or "0"),
                Axis = _axes.Value((string?)element.Attribute(Calc("axis-position")) ?? "automatic"),
                AxisColorHex = Upper((string?)element.Attribute(Calc("axis-color"))) ?? "#000000",
                MinimumLength = Length(element.Attribute(Calc("min-length")), 0),
                MaximumLength = Length(element.Attribute(Calc("max-length")), 100),
            },
        };
    }

    private double Length(XAttribute? attribute, double fallback) =>
        double.TryParse((string?)attribute, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= 100 ? value : fallback;
}

internal sealed class ColorScaleCodec(OdfThresholds thresholds) : ConditionalRuleCodec
{
    public override XName Element => Calc("color-scale");

    public override bool Writes(ConditionalRule rule) => rule.Visual == ConditionalVisual.ColorScale;

    public override XElement Write(Worksheet sheet, ConditionalRule rule)
    {
        var scale = new XElement(Element);
        var colors = rule.ScaleColors;
        var explicitStops = rule.Thresholds.Count == colors.Count;

        for (var i = 0; i < colors.Count; i++)
        {
            var threshold = explicitStops ? rule.Thresholds[i]
                : i == 0 ? new ConditionalThreshold(ConditionalThresholdKind.Minimum)
                : i == colors.Count - 1 ? new ConditionalThreshold(ConditionalThresholdKind.Maximum)
                : new ConditionalThreshold(ConditionalThresholdKind.Percentile, 100.0 * i / (colors.Count - 1));
            var entry = new XElement(Calc("color-scale-entry"));
            thresholds.Write(entry, threshold);
            entry.Add(new XAttribute(Calc("color"), colors[i].ToLowerInvariant()));
            scale.Add(entry);
        }
        return scale;
    }

    public override ConditionalRule Read(XElement element, RuleReadContext context)
    {
        var entries = element.Elements(Calc("color-scale-entry")).ToList();
        var colors = entries.Select(entry => Upper((string?)entry.Attribute(Calc("color"))) ?? "#FFFFFF").ToList();

        return new ConditionalRule
        {
            Range = context.Range,
            Priority = context.Priority,
            Visual = ConditionalVisual.ColorScale,
            ScaleColors = colors.Count >= 2 ? colors : ["#F8696B", "#63BE7B"],
            Thresholds = colors.Count >= 2 ? [.. entries.Select(thresholds.Read)] : [],
        };
    }
}

internal sealed class IconSetCodec(OdfThresholds thresholds) : ConditionalRuleCodec
{
    public override XName Element => Calc("icon-set");

    public override bool Writes(ConditionalRule rule) => rule.Visual == ConditionalVisual.IconSet;

    public override XElement Write(Worksheet sheet, ConditionalRule rule)
    {
        var set = new XElement(Element, new XAttribute(Calc("icon-set-type"), rule.IconSetName));
        if (rule.HasCustomIcons)
        {
            set.Add(new XAttribute(Calc("custom"), "true"));
            foreach (var icon in rule.CustomIcons)
                set.Add(new XElement(Calc("custom-iconset"),
                    new XAttribute(Calc("custom-iconset-name"), icon.IsNone ? ConditionalIconChoice.NoIcons : icon.Set),
                    new XAttribute(Calc("custom-iconset-index"), (icon.IsNone ? -1 : icon.Index).ToString(CultureInfo.InvariantCulture))));
        }
        if (!rule.ShowValue) set.Add(new XAttribute(Calc("show-value"), "false"));

        var count = rule.IconCount;
        var entries = rule.Thresholds.Count == count
            ? rule.Thresholds
            : [.. Enumerable.Range(0, count).Select(index => new ConditionalThreshold(ConditionalThresholdKind.Percent, 100.0 * index / count))];
        foreach (var threshold in entries)
        {
            var entry = new XElement(Calc("formatting-entry"));
            thresholds.Write(entry, threshold);
            set.Add(entry);
        }
        return set;
    }

    public override ConditionalRule Read(XElement element, RuleReadContext context) => new()
    {
        Range = context.Range,
        Priority = context.Priority,
        Visual = ConditionalVisual.IconSet,
        IconSetName = (string?)element.Attribute(Calc("icon-set-type")) ?? "3Arrows",
        ShowValue = (string?)element.Attribute(Calc("show-value")) != "false",
        Thresholds = [.. element.Elements(Calc("formatting-entry")).Select(thresholds.Read)],
        CustomIcons = (string?)element.Attribute(Calc("custom")) is "true" or "1"
            ? [.. element.Elements(Calc("custom-iconset")).Select(Icon)]
            : [],
    };

    private ConditionalIconChoice Icon(XElement element) => new(
        (string?)element.Attribute(Calc("custom-iconset-name")) ?? ConditionalIconChoice.NoIcons,
        int.TryParse((string?)element.Attribute(Calc("custom-iconset-index")), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : -1);
}

internal sealed class ConditionCodec(OdfConditionSyntax syntax, IConditionStyles styles, OdfRangeAddresses addresses) : ConditionalRuleCodec
{
    public override XName Element => Calc("condition");

    public override bool Writes(ConditionalRule rule) => true;

    public override XElement Write(Worksheet sheet, ConditionalRule rule) =>
        new(Element,
            new XAttribute(Calc("apply-style-name"), styles.NameFor(rule.Style)),
            new XAttribute(Calc("value"), syntax.Write(rule)),
            new XAttribute(Calc("base-cell-address"), addresses.WriteAddress(sheet.Name, rule.Range.Start)));

    public override ConditionalRule Read(XElement element, RuleReadContext context)
    {
        var styleName = (string?)element.Attribute(Calc("apply-style-name"));
        var style = styleName is not null && context.NamedStyles.TryGetValue(styleName, out var found) ? found : CellStyle.Default;
        var parsed = syntax.Read((string?)element.Attribute(Calc("value")) ?? string.Empty);
        var formula = parsed.Formula is { } written && addresses.Read((string?)element.Attribute(Calc("base-cell-address"))) is [var baseCell, ..] && baseCell.Start != context.Range.Start
            ? _translator.Translate(written, context.Range.Start.Row - baseCell.Start.Row, context.Range.Start.Column - baseCell.Start.Column) ?? written
            : parsed.Formula;

        return new ConditionalRule
        {
            Range = context.Range,
            Priority = context.Priority,
            Operator = parsed.Operator,
            Operands = parsed.Operands,
            Formula = formula,
            Style = style,
        };
    }

    private readonly PulseOffice.Sheets.Engine.Operations.FormulaTranslator _translator = new();
}

internal sealed class DateIsCodec(IConditionStyles styles) : ConditionalRuleCodec
{
    private readonly TokenMap<DatePeriod> _periods = new(
        DatePeriod.Today, "today",
        (DatePeriod.Today, "today"), (DatePeriod.Yesterday, "yesterday"), (DatePeriod.Tomorrow, "tomorrow"),
        (DatePeriod.Last7Days, "last-7-days"),
        (DatePeriod.ThisWeek, "this-week"), (DatePeriod.LastWeek, "last-week"), (DatePeriod.NextWeek, "next-week"),
        (DatePeriod.ThisMonth, "this-month"), (DatePeriod.LastMonth, "last-month"), (DatePeriod.NextMonth, "next-month"),
        (DatePeriod.ThisYear, "this-year"), (DatePeriod.LastYear, "last-year"), (DatePeriod.NextYear, "next-year"));

    public override XName Element => Calc("date-is");

    public override bool Writes(ConditionalRule rule) => rule.Visual == ConditionalVisual.Style && rule.Formula is null && rule.Operator == ConditionOperator.DateIs;

    public override XElement Write(Worksheet sheet, ConditionalRule rule) =>
        new(Element,
            new XAttribute(Calc("style"), styles.NameFor(rule.Style)),
            new XAttribute(Calc("date"), _periods.Token(rule.DatePeriod)));

    public override ConditionalRule Read(XElement element, RuleReadContext context)
    {
        var styleName = (string?)element.Attribute(Calc("style"));
        return new ConditionalRule
        {
            Range = context.Range,
            Priority = context.Priority,
            Operator = ConditionOperator.DateIs,
            DatePeriod = _periods.Value((string?)element.Attribute(Calc("date")) ?? "today"),
            Style = styleName is not null && context.NamedStyles.TryGetValue(styleName, out var found) ? found : CellStyle.Default,
        };
    }
}

internal sealed class ConditionStyleSheet : IConditionStyles
{
    private readonly List<XElement> _styles = [];
    private readonly Dictionary<CellStyle, string> _index = [];
    private readonly OdfCellStyleBuilder _builder = new(new OdfCellStyleProperties());

    public IReadOnlyList<XElement> Styles => _styles;

    public string NameFor(CellStyle style)
    {
        if (_index.TryGetValue(style, out var existing)) return existing;

        var name = $"Condition{_styles.Count + 1}";
        _styles.Add(_builder.Build(name, style));
        _index[style] = name;
        return name;
    }
}

internal sealed class OdfRangeAddresses
{
    private readonly OdfQualifiedRanges _ranges = new();

    public string Write(string sheet, CellRange range) => _ranges.Write(sheet, range);

    public string WriteAddress(string sheet, CellAddress address) => _ranges.WriteAddress(sheet, address);

    public IReadOnlyList<CellRange> Read(string? target) => [.. _ranges.ReadAll(target).Select(found => found.Range)];
}


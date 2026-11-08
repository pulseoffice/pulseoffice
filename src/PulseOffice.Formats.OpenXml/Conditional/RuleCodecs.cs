using System.Globalization;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using CellStyle = PulseOffice.Sheets.Styling.CellStyle;
using Color = DocumentFormat.OpenXml.Spreadsheet.Color;

namespace PulseOffice.Formats.OpenXml;

internal sealed class DifferentialFormatTable(SpreadsheetColours colours)
{
    private readonly DifferentialFormats _formats = new();
    private readonly Dictionary<string, uint> _index = new(StringComparer.Ordinal);
    private uint _count;

    public DifferentialFormats Element
    {
        get
        {
            _formats.Count = (uint)_formats.ChildElements.Count;
            return _formats;
        }
    }

    public bool Any => _formats.ChildElements.Count > 0;

    public uint Intern(CellStyle style)
    {
        var key = $"{style.BackgroundHex}|{style.Font.ColorHex}|{style.Font.Bold}|{style.Font.Italic}";
        if (_index.TryGetValue(key, out var existing)) return existing;

        var format = new DifferentialFormat();
        if (style.Font.Bold || style.Font.Italic || style.Font.ColorHex != CellFont.Default.ColorHex)
        {
            var font = new Font();
            if (style.Font.Bold) font.AppendChild(new Bold());
            if (style.Font.Italic) font.AppendChild(new Italic());
            if (style.Font.ColorHex != CellFont.Default.ColorHex) font.AppendChild(new Color { Rgb = colours.ToArgb(style.Font.ColorHex) });
            format.AppendChild(font);
        }

        if (style.BackgroundHex is { } background)
            format.AppendChild(new Fill(new PatternFill
            {
                PatternType = PatternValues.Solid,
                BackgroundColor = new BackgroundColor { Rgb = colours.ToArgb(background) },
            }));

        var id = _count++;
        _formats.AppendChild(format);
        _index[key] = id;
        return id;
    }

    public uint InternColour(string hex)
    {
        var key = "colour|" + hex;
        if (_index.TryGetValue(key, out var existing)) return existing;

        var argb = colours.ToArgb(hex);
        var format = new DifferentialFormat(
            new Font(new Color { Rgb = argb }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = argb }, new BackgroundColor { Rgb = argb }) { PatternType = PatternValues.Solid }));
        var id = _count++;
        _formats.AppendChild(format);
        _index[key] = id;
        return id;
    }

    public CellStyle Read(IReadOnlyList<DifferentialFormat> formats, uint? id)
    {
        if (id is not { } value || value >= formats.Count) return CellStyle.Default;

        var format = formats[(int)value];
        var font = format.Font;
        return new CellStyle
        {
            BackgroundHex = colours.Parse(format.Fill?.PatternFill?.BackgroundColor?.Rgb?.Value),
            Font = CellFont.Default with
            {
                Bold = font?.Bold is not null,
                Italic = font?.Italic is not null,
                ColorHex = colours.Parse(font?.Color?.Rgb?.Value) ?? CellFont.Default.ColorHex,
            },
        };
    }
}

internal sealed record XlsxRuleReadContext(CellRange Range, IReadOnlyList<DifferentialFormat> Formats, DifferentialFormatTable Table)
{
    public int Priority(ConditionalFormattingRule element) => Math.Max(element.Priority?.Value ?? 1, 1) - 1;

    public IReadOnlyList<string> Expressions(ConditionalFormattingRule element) =>
        [.. element.Elements<Formula>().Select(formula => formula.Text.Trim())];
}

internal abstract class XlsxRuleCodec(SpreadsheetColours colours)
{
    protected SpreadsheetColours Colours { get; } = colours;

    public abstract bool Writes(ConditionalRule rule);

    public abstract bool Reads(ConditionalFormattingRule element);

    public ConditionalFormattingRule Write(ConditionalRule rule)
    {
        var element = new ConditionalFormattingRule { Priority = (int)Math.Min((long)rule.Priority + 1, int.MaxValue) };
        Fill(element, rule);
        return element;
    }

    protected abstract void Fill(ConditionalFormattingRule element, ConditionalRule rule);

    public abstract ConditionalRule Read(ConditionalFormattingRule element, XlsxRuleReadContext context);
}

internal sealed class XlsxThresholds
{
    private readonly XlsxFormulaSyntax _formulas = new();

    public ConditionalFormatValueObject Write(ConditionalThreshold threshold)
    {
        var element = Element(threshold);
        if (!threshold.GreaterOrEqual) element.GreaterThanOrEqual = false;
        return element;
    }

    public ConditionalThreshold Read(ConditionalFormatValueObject element) =>
        Kind(element) with { GreaterOrEqual = element.GreaterThanOrEqual?.Value ?? true };

    private ConditionalFormatValueObject Element(ConditionalThreshold threshold) => threshold.Kind switch
    {
        ConditionalThresholdKind.Minimum or ConditionalThresholdKind.AutomaticMinimum => new ConditionalFormatValueObject { Type = ConditionalFormatValueObjectValues.Min },
        ConditionalThresholdKind.Maximum or ConditionalThresholdKind.AutomaticMaximum => new ConditionalFormatValueObject { Type = ConditionalFormatValueObjectValues.Max },
        ConditionalThresholdKind.Percent => Valued(ConditionalFormatValueObjectValues.Percent, threshold.Value),
        ConditionalThresholdKind.Percentile => Valued(ConditionalFormatValueObjectValues.Percentile, threshold.Value),
        ConditionalThresholdKind.Formula when threshold.Formula is { } formula =>
            new ConditionalFormatValueObject { Type = ConditionalFormatValueObjectValues.Formula, Val = _formulas.ToOpenXml(formula) },
        _ => Valued(ConditionalFormatValueObjectValues.Number, threshold.Value),
    };

    private ConditionalThreshold Kind(ConditionalFormatValueObject element)
    {
        var text = element.Val?.Value ?? "0";
        var number = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? (double.IsFinite(parsed) ? parsed : 0) : (double?)null;
        var type = element.Type?.Value;
        if (type == ConditionalFormatValueObjectValues.Min) return new(ConditionalThresholdKind.Minimum);
        if (type == ConditionalFormatValueObjectValues.Max) return new(ConditionalThresholdKind.Maximum);
        if (type == ConditionalFormatValueObjectValues.Percent) return new(ConditionalThresholdKind.Percent, number ?? 0);
        if (type == ConditionalFormatValueObjectValues.Percentile) return new(ConditionalThresholdKind.Percentile, number ?? 0);
        if (number is { } constant) return new(ConditionalThresholdKind.Number, constant);
        return new(ConditionalThresholdKind.Formula, 0, _formulas.ToInternal(text).TrimStart('='));
    }

    private ConditionalFormatValueObject Valued(ConditionalFormatValueObjectValues type, double value) =>
        new() { Type = type, Val = (double.IsFinite(value) ? value : 0).ToString("R", CultureInfo.InvariantCulture) };
}

internal sealed class XlsxDataBarCodec(SpreadsheetColours colours, XlsxThresholds thresholds, X14RuleIds ids) : XlsxRuleCodec(colours)
{
    public override bool Writes(ConditionalRule rule) => rule.Visual == ConditionalVisual.DataBar;

    public override bool Reads(ConditionalFormattingRule element) => element.Type?.Value == ConditionalFormatValues.DataBar;

    public string? ExtensionId(ConditionalFormattingRule element) =>
        element.GetFirstChild<ConditionalFormattingRuleExtensionList>()?.Elements<ConditionalFormattingRuleExtension>()
            .Where(extension => string.Equals(extension.Uri?.Value, X14DataBarCodec.RuleExtensionUri, StringComparison.OrdinalIgnoreCase))
            .SelectMany(extension => extension.Descendants())
            .FirstOrDefault(child => child.LocalName == "id")?.InnerText.Trim();

    protected override void Fill(ConditionalFormattingRule element, ConditionalRule rule)
    {
        element.Type = ConditionalFormatValues.DataBar;
        var stops = rule.Thresholds.Count >= 2
            ? rule.Thresholds.Take(2)
            : [new ConditionalThreshold(ConditionalThresholdKind.Minimum), new ConditionalThreshold(ConditionalThresholdKind.Maximum)];
        var bar = new DataBar
        {
            MinLength = (uint)Math.Round(Math.Clamp(rule.Bar.MinimumLength, 0, 100)),
            MaxLength = (uint)Math.Round(Math.Clamp(rule.Bar.MaximumLength, 0, 100)),
        };
        if (!rule.ShowValue) bar.ShowValue = false;
        foreach (var stop in stops) bar.AppendChild(thresholds.Write(stop));
        bar.AppendChild(new Color { Rgb = Colours.ToArgb(rule.BarColorHex) });
        element.AppendChild(bar);

        var link = new ConditionalFormattingRuleExtension { Uri = X14DataBarCodec.RuleExtensionUri };
        link.AddNamespaceDeclaration("x14", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main");
        link.AppendChild(new DocumentFormat.OpenXml.Office2010.Excel.Id(ids.For(rule)));
        element.AppendChild(new ConditionalFormattingRuleExtensionList(link));
    }

    public override ConditionalRule Read(ConditionalFormattingRule element, XlsxRuleReadContext context)
    {
        var bar = element.GetFirstChild<DataBar>();
        var stops = bar?.Elements<ConditionalFormatValueObject>().Select(thresholds.Read).ToList() ?? [];
        return new()
        {
            Range = context.Range,
            Priority = context.Priority(element),
            Visual = ConditionalVisual.DataBar,
            BarColorHex = Colours.Parse(bar?.GetFirstChild<Color>()?.Rgb?.Value) ?? "#638EC6",
            ShowValue = bar?.ShowValue?.Value ?? true,
            Thresholds = stops is [{ Kind: ConditionalThresholdKind.Minimum }, { Kind: ConditionalThresholdKind.Maximum }] ? [] : stops,
            Bar = new DataBarOptions
            {
                MinimumLength = Math.Min(bar?.MinLength?.Value ?? 10, 100),
                MaximumLength = Math.Min(bar?.MaxLength?.Value ?? 90, 100),
            },
        };
    }
}

internal sealed class XlsxColorScaleCodec(SpreadsheetColours colours, XlsxThresholds thresholds) : XlsxRuleCodec(colours)
{
    public override bool Writes(ConditionalRule rule) => rule.Visual == ConditionalVisual.ColorScale;

    public override bool Reads(ConditionalFormattingRule element) => element.Type?.Value == ConditionalFormatValues.ColorScale;

    protected override void Fill(ConditionalFormattingRule element, ConditionalRule rule)
    {
        element.Type = ConditionalFormatValues.ColorScale;

        var colors = rule.ScaleColors.Take(3).ToList();
        var stops = rule.Thresholds.Count == rule.ScaleColors.Count && colors.Count == rule.ScaleColors.Count
            ? rule.Thresholds
            : [.. Enumerable.Range(0, colors.Count).Select(index =>
                index == 0 ? new ConditionalThreshold(ConditionalThresholdKind.Minimum)
                : index == colors.Count - 1 ? new ConditionalThreshold(ConditionalThresholdKind.Maximum)
                : new ConditionalThreshold(ConditionalThresholdKind.Percentile, 50))];

        var scale = new ColorScale();
        foreach (var stop in stops) scale.AppendChild(thresholds.Write(stop));
        foreach (var color in colors) scale.AppendChild(new Color { Rgb = Colours.ToArgb(color) });

        element.AppendChild(scale);
    }

    public override ConditionalRule Read(ConditionalFormattingRule element, XlsxRuleReadContext context)
    {
        var scale = element.GetFirstChild<ColorScale>();
        var colors = scale?.Elements<Color>().Select(color => Colours.Parse(color.Rgb?.Value) ?? "#FFFFFF").ToList() ?? [];
        var stops = scale?.Elements<ConditionalFormatValueObject>().Select(thresholds.Read).ToList() ?? [];

        return new ConditionalRule
        {
            Range = context.Range,
            Priority = context.Priority(element),
            Visual = ConditionalVisual.ColorScale,
            ScaleColors = colors.Count >= 2 ? colors : ["#F8696B", "#63BE7B"],
            Thresholds = colors.Count >= 2 && stops.Count == colors.Count ? stops : [],
        };
    }
}

internal sealed class XlsxIconSetCodec(SpreadsheetColours colours, XlsxThresholds thresholds, XlsxIconSets sets) : XlsxRuleCodec(colours)
{
    public override bool Writes(ConditionalRule rule) => rule.Visual == ConditionalVisual.IconSet;

    public override bool Reads(ConditionalFormattingRule element) => element.Type?.Value == ConditionalFormatValues.IconSet;

    protected override void Fill(ConditionalFormattingRule element, ConditionalRule rule)
    {
        element.Type = ConditionalFormatValues.IconSet;
        var set = new IconSet { IconSetValue = new IconSetValues(sets.Writable(rule)) };
        if (!rule.ShowValue) set.ShowValue = false;
        if (rule.ReverseIcons) set.Reverse = true;

        var count = rule.IconCount;
        var stops = rule.Thresholds.Count == count
            ? rule.Thresholds
            : [.. Enumerable.Range(0, count).Select(index => new ConditionalThreshold(ConditionalThresholdKind.Percent, 100.0 * index / count))];
        foreach (var stop in stops) set.AppendChild(thresholds.Write(stop));
        element.AppendChild(set);
    }

    public override ConditionalRule Read(ConditionalFormattingRule element, XlsxRuleReadContext context)
    {
        var set = element.GetFirstChild<IconSet>();
        return new ConditionalRule
        {
            Range = context.Range,
            Priority = context.Priority(element),
            Visual = ConditionalVisual.IconSet,
            IconSetName = set?.IconSetValue?.InnerText is { Length: > 0 } name ? name : "3TrafficLights1",
            ShowValue = set?.ShowValue?.Value ?? true,
            ReverseIcons = set?.Reverse?.Value ?? false,
            Thresholds = [.. set?.Elements<ConditionalFormatValueObject>().Select(thresholds.Read) ?? []],
        };
    }
}

internal abstract class StyledRuleCodec(SpreadsheetColours colours, DifferentialFormatTable formats) : XlsxRuleCodec(colours)
{
    protected override void Fill(ConditionalFormattingRule element, ConditionalRule rule)
    {
        element.FormatId = formats.Intern(rule.Style);
        element.StopIfTrue = rule.StopIfTrue ? true : null;
        Condition(element, rule);
    }

    protected abstract void Condition(ConditionalFormattingRule element, ConditionalRule rule);

    public override ConditionalRule Read(ConditionalFormattingRule element, XlsxRuleReadContext context) =>
        Condition(element, context, new ConditionalRule
        {
            Range = context.Range,
            Priority = context.Priority(element),
            Style = context.Table.Read(context.Formats, element.FormatId?.Value),
            StopIfTrue = element.StopIfTrue?.Value ?? false,
        });

    protected abstract ConditionalRule Condition(ConditionalFormattingRule element, XlsxRuleReadContext context, ConditionalRule rule);
}

internal sealed class XlsxExpressionCodec(SpreadsheetColours colours, DifferentialFormatTable formats) : StyledRuleCodec(colours, formats)
{
    public override bool Writes(ConditionalRule rule) => rule.Formula is { Length: > 0 };

    public override bool Reads(ConditionalFormattingRule element) => element.Type?.Value == ConditionalFormatValues.Expression;

    protected override void Condition(ConditionalFormattingRule element, ConditionalRule rule)
    {
        element.Type = ConditionalFormatValues.Expression;
        element.AppendChild(new Formula(new XlsxFormulaSyntax().ToOpenXml(rule.Formula!)));
    }

    protected override ConditionalRule Condition(ConditionalFormattingRule element, XlsxRuleReadContext context, ConditionalRule rule) =>
        context.Expressions(element) is [var first, ..] ? rule with { Formula = new XlsxFormulaSyntax().ToInternal(first) } : rule;
}

internal sealed class XlsxOperatorCodec(SpreadsheetColours colours, DifferentialFormatTable formats, XlsxComparisonOperands comparisons) : StyledRuleCodec(colours, formats)
{
    private readonly IReadOnlyList<(ConditionOperator Operator, ConditionalFormatValues Type, ConditionalFormattingOperatorValues? Sdk)> _table =
    [
        (ConditionOperator.Equal, ConditionalFormatValues.CellIs, ConditionalFormattingOperatorValues.Equal),
        (ConditionOperator.NotEqual, ConditionalFormatValues.CellIs, ConditionalFormattingOperatorValues.NotEqual),
        (ConditionOperator.GreaterThan, ConditionalFormatValues.CellIs, ConditionalFormattingOperatorValues.GreaterThan),
        (ConditionOperator.GreaterOrEqual, ConditionalFormatValues.CellIs, ConditionalFormattingOperatorValues.GreaterThanOrEqual),
        (ConditionOperator.LessThan, ConditionalFormatValues.CellIs, ConditionalFormattingOperatorValues.LessThan),
        (ConditionOperator.LessOrEqual, ConditionalFormatValues.CellIs, ConditionalFormattingOperatorValues.LessThanOrEqual),
        (ConditionOperator.Between, ConditionalFormatValues.CellIs, ConditionalFormattingOperatorValues.Between),
        (ConditionOperator.NotBetween, ConditionalFormatValues.CellIs, ConditionalFormattingOperatorValues.NotBetween),
        (ConditionOperator.ContainsText, ConditionalFormatValues.ContainsText, null),
        (ConditionOperator.NotContainsText, ConditionalFormatValues.NotContainsText, null),
        (ConditionOperator.BeginsWith, ConditionalFormatValues.BeginsWith, null),
        (ConditionOperator.EndsWith, ConditionalFormatValues.EndsWith, null),
        (ConditionOperator.IsBlank, ConditionalFormatValues.ContainsBlanks, null),
        (ConditionOperator.IsNotBlank, ConditionalFormatValues.NotContainsBlanks, null),
        (ConditionOperator.IsError, ConditionalFormatValues.ContainsErrors, null),
        (ConditionOperator.IsNotError, ConditionalFormatValues.NotContainsErrors, null),
    ];

    public override bool Writes(ConditionalRule rule) => true;

    public override bool Reads(ConditionalFormattingRule element) => true;

    protected override void Condition(ConditionalFormattingRule element, ConditionalRule rule)
    {
        var entry = _table.FirstOrDefault(row => row.Operator == rule.Operator, _table[^1]);
        element.Type = entry.Type;
        if (entry.Sdk is { } sdk) element.Operator = sdk;

        if (_textForms.TryGetValue(rule.Operator, out var textForm))
        {
            var text = rule.Operands.Count > 0 ? rule.Operands[0] : string.Empty;
            element.Text = text;
            element.AppendChild(new Formula(textForm(rule.Anchor.ToA1(), Quote(text, true))));
            return;
        }
        if (_stateForms.TryGetValue(rule.Operator, out var stateForm))
        {
            element.AppendChild(new Formula(stateForm(rule.Anchor.ToA1())));
            return;
        }

        foreach (var operand in rule.Operands) element.AppendChild(new Formula(Quote(operand)));
    }

    private readonly IReadOnlyDictionary<ConditionOperator, Func<string, string, string>> _textForms = new Dictionary<ConditionOperator, Func<string, string, string>>
    {
        [ConditionOperator.ContainsText] = (cell, text) => $"NOT(ISERROR(SEARCH({text},{cell})))",
        [ConditionOperator.NotContainsText] = (cell, text) => $"ISERROR(SEARCH({text},{cell}))",
        [ConditionOperator.BeginsWith] = (cell, text) => $"LEFT({cell},LEN({text}))={text}",
        [ConditionOperator.EndsWith] = (cell, text) => $"RIGHT({cell},LEN({text}))={text}",
    };

    private readonly IReadOnlyDictionary<ConditionOperator, Func<string, string>> _stateForms = new Dictionary<ConditionOperator, Func<string, string>>
    {
        [ConditionOperator.IsBlank] = cell => $"LEN(TRIM({cell}))=0",
        [ConditionOperator.IsNotBlank] = cell => $"LEN(TRIM({cell}))>0",
        [ConditionOperator.IsError] = cell => $"ISERROR({cell})",
        [ConditionOperator.IsNotError] = cell => $"NOT(ISERROR({cell}))",
    };

    protected override ConditionalRule Condition(ConditionalFormattingRule element, XlsxRuleReadContext context, ConditionalRule rule)
    {
        var type = element.Type?.Value;
        var sdk = element.Operator?.Value;
        var match = _table.Where(row => row.Sdk is null ? row.Type == type : sdk is not null && row.Sdk == sdk)
            .Select(row => (ConditionOperator?)row.Operator).FirstOrDefault();

        var op = match ?? ConditionOperator.GreaterThan;
        if (_textForms.ContainsKey(op) && element.Text?.Value is { } text) return rule with { Operator = op, Operands = [text] };
        if (_stateForms.ContainsKey(op)) return rule with { Operator = op };
        return comparisons.Apply(rule, op, context.Expressions(element));
    }

    private string Quote(string operand, bool alwaysText = false) =>
        !alwaysText && double.TryParse(operand, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
            ? operand
            : $"\"{operand.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

internal sealed class XlsxIconSets
{
    private readonly HashSet<string> _native = new(StringComparer.Ordinal)
    {
        "3Arrows", "3ArrowsGray", "3Flags", "3TrafficLights1", "3TrafficLights2", "3Signs", "3Symbols", "3Symbols2",
        "4Arrows", "4ArrowsGray", "4RedToBlack", "4Rating", "4TrafficLights",
        "5Arrows", "5ArrowsGray", "5Rating", "5Quarters",
    };

    private readonly HashSet<string> _extended = new(StringComparer.Ordinal) { "3Stars", "3Triangles", "5Boxes" };

    public bool IsNative(string name) => _native.Contains(name);

    public bool IsExtended(ConditionalRule rule) =>
        rule.Visual == ConditionalVisual.IconSet && (_extended.Contains(rule.IconSetName) || rule.HasCustomIcons);

    public bool IsLost(ConditionalRule rule) =>
        rule.Visual == ConditionalVisual.IconSet && (rule.HasCustomIcons
            ? rule.CustomIcons.Any(icon => !icon.IsNone && !Known(icon.Set))
            : !Known(rule.IconSetName));

    private bool Known(string set) => IsNative(set) || _extended.Contains(set);

    public string Writable(ConditionalRule rule) => IsNative(rule.IconSetName) ? rule.IconSetName : $"{rule.IconCount}Arrows";
}

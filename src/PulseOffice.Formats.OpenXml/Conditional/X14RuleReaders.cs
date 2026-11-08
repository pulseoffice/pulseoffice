using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Formats.OpenXml;

internal sealed class X14Namespaces
{
    public XNamespace X14 { get; } = "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main";

    public XNamespace Xm { get; } = "http://schemas.microsoft.com/office/excel/2006/main";

    public XNamespace Main { get; } = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public IReadOnlyList<CellRange> Ranges(XElement owner) =>
        [.. ((string?)owner.Element(Xm + "sqref") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(text => CellRange.TryParseA1(text.Replace("$", string.Empty, StringComparison.Ordinal), out var range) ? range : (CellRange?)null)
            .OfType<CellRange>()];

    public bool IsTrue(XAttribute? attribute) => (string?)attribute is "1" or "true";

    public bool IsFalse(XAttribute? attribute) => (string?)attribute is "0" or "false";
}

internal sealed class X14ThresholdVocabulary
{
    public TokenMap<ConditionalThresholdKind> Kinds { get; } = new(
        ConditionalThresholdKind.Number, "num",
        (ConditionalThresholdKind.Minimum, "min"),
        (ConditionalThresholdKind.Maximum, "max"),
        (ConditionalThresholdKind.Number, "num"),
        (ConditionalThresholdKind.Percent, "percent"),
        (ConditionalThresholdKind.Percentile, "percentile"),
        (ConditionalThresholdKind.Formula, "formula"),
        (ConditionalThresholdKind.AutomaticMinimum, "autoMin"),
        (ConditionalThresholdKind.AutomaticMaximum, "autoMax"));
}

internal interface IX14RuleReader
{
    bool Reads(string type);

    ConditionalRule? Read(XElement rule, IReadOnlyList<CellRange> ranges);
}

internal sealed class X14IconSetRuleReader(X14Namespaces names, X14Thresholds thresholds) : IX14RuleReader
{
    public bool Reads(string type) => type == "iconSet";

    public ConditionalRule? Read(XElement rule, IReadOnlyList<CellRange> ranges)
    {
        if (rule.Element(names.X14 + "iconSet") is not { } set) return null;

        return new ConditionalRule
        {
            Range = ranges[0],
            AdditionalRanges = [.. ranges.Skip(1)],
            Priority = (int.TryParse((string?)rule.Attribute("priority"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority) && priority > 0 ? priority : 1) - 1,
            Visual = ConditionalVisual.IconSet,
            IconSetName = (string?)set.Attribute("iconSet") is { Length: > 0 } name ? name : "3TrafficLights1",
            ShowValue = !names.IsFalse(set.Attribute("showValue")),
            ReverseIcons = names.IsTrue(set.Attribute("reverse")),
            Thresholds = [.. set.Elements(names.X14 + "cfvo").Select(thresholds.Read)],
            CustomIcons = names.IsTrue(set.Attribute("custom")) ? [.. set.Elements(names.X14 + "cfIcon").Select(Icon)] : [],
        };
    }

    private ConditionalIconChoice Icon(XElement element)
    {
        var set = (string?)element.Attribute("iconSet") ?? ConditionalIconChoice.NoIcons;
        var index = int.TryParse((string?)element.Attribute("iconId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
        return new ConditionalIconChoice(set, set == ConditionalIconChoice.NoIcons ? -1 : index);
    }
}

internal sealed class X14StyledRuleReader(X14Namespaces names, XlsxFormulaSyntax formulas, DifferentialFormatTable table, XlsxComparisonOperands comparisons) : IX14RuleReader
{
    private readonly IReadOnlyDictionary<string, ConditionOperator> _comparisons = new Dictionary<string, ConditionOperator>(StringComparer.Ordinal)
    {
        ["equal"] = ConditionOperator.Equal, ["notEqual"] = ConditionOperator.NotEqual,
        ["greaterThan"] = ConditionOperator.GreaterThan, ["greaterThanOrEqual"] = ConditionOperator.GreaterOrEqual,
        ["lessThan"] = ConditionOperator.LessThan, ["lessThanOrEqual"] = ConditionOperator.LessOrEqual,
        ["between"] = ConditionOperator.Between, ["notBetween"] = ConditionOperator.NotBetween,
    };

    private readonly IReadOnlyDictionary<string, ConditionOperator> _texts = new Dictionary<string, ConditionOperator>(StringComparer.Ordinal)
    {
        ["containsText"] = ConditionOperator.ContainsText, ["notContainsText"] = ConditionOperator.NotContainsText,
        ["beginsWith"] = ConditionOperator.BeginsWith, ["endsWith"] = ConditionOperator.EndsWith,
    };

    private readonly IReadOnlyDictionary<string, ConditionOperator> _states = new Dictionary<string, ConditionOperator>(StringComparer.Ordinal)
    {
        ["containsBlanks"] = ConditionOperator.IsBlank, ["notContainsBlanks"] = ConditionOperator.IsNotBlank,
        ["containsErrors"] = ConditionOperator.IsError, ["notContainsErrors"] = ConditionOperator.IsNotError,
    };

    public bool Reads(string type) => type is "expression" or "cellIs" || _texts.ContainsKey(type) || _states.ContainsKey(type);

    public ConditionalRule? Read(XElement rule, IReadOnlyList<CellRange> ranges)
    {
        var type = (string?)rule.Attribute("type") ?? string.Empty;
        var expressions = rule.Elements(names.Xm + "f").Select(element => element.Value.Trim()).ToList();
        var basis = new ConditionalRule
        {
            Range = ranges[0],
            AdditionalRanges = [.. ranges.Skip(1)],
            Priority = (int.TryParse((string?)rule.Attribute("priority"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority) && priority > 0 ? priority : 1) - 1,
            Style = Style(rule.Element(names.X14 + "dxf")),
            StopIfTrue = names.IsTrue(rule.Attribute("stopIfTrue")),
        };

        if (_states.TryGetValue(type, out var state)) return basis with { Operator = state };
        if (_texts.TryGetValue(type, out var text)) return Text(basis, text, (string?)rule.Attribute("text"), expressions);
        if (type == "cellIs") return Comparison(basis, (string?)rule.Attribute("operator") ?? "equal", expressions);
        return expressions is [var first, ..] ? basis with { Formula = formulas.ToInternal(first) } : null;
    }

    private ConditionalRule? Text(ConditionalRule basis, ConditionOperator op, string? text, IReadOnlyList<string> expressions)
    {
        if (expressions.Count < 2 && text is not null) return basis with { Operator = op, Operands = [text] };
        if (expressions.Count >= 2 && comparisons.Literal(expressions[1]) is { } literal) return basis with { Operator = op, Operands = [literal] };
        return expressions is [var first, ..] ? basis with { Formula = formulas.ToInternal(first) } : null;
    }

    private ConditionalRule? Comparison(ConditionalRule basis, string token, IReadOnlyList<string> expressions) =>
        _comparisons.TryGetValue(token, out var op) && expressions.Count > 0 ? comparisons.Apply(basis, op, expressions) : null;

    private PulseOffice.Sheets.Styling.CellStyle Style(XElement? dxf)
    {
        if (dxf is null) return PulseOffice.Sheets.Styling.CellStyle.Default;

        var element = new XElement(names.Main + "dxf", dxf.Elements());
        return table.Read([new DifferentialFormat(element.ToString(SaveOptions.DisableFormatting))], 0);
    }
}

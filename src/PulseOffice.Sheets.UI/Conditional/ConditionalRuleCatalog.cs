using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.UI.Conditional;

public enum ConditionalRuleKind { CellValue, TopBottom, Average, Duplicates, Text, Date, Blanks, Formula, DataBar, ColorScale, IconSet }

public sealed record ConditionalStylePreset(string Key, string? BackgroundHex, string FontHex, bool Bold = false);

public sealed record ColorScalePreset(string Key, IReadOnlyList<string> Colors);

public interface IConditionalRuleCatalog
{
    IReadOnlyList<ConditionalRuleKind> Kinds { get; }

    IReadOnlyList<ConditionalStylePreset> StylePresets { get; }

    IReadOnlyList<ColorScalePreset> ScalePresets { get; }

    IReadOnlyList<string> BarColors { get; }

    IReadOnlyList<ConditionOperator> Operators(ConditionalRuleKind kind);

    ConditionalRuleKind KindOf(ConditionalRule rule);

    bool IsStyled(ConditionalRuleKind kind);

    ConditionalStylePreset? PresetOf(CellStyle style);

    CellStyle StyleOf(ConditionalStylePreset preset);

    ConditionalRule Default(ConditionalRuleKind kind, Addressing.CellRange range);

    string Describe(ConditionalRule rule, ITextCatalog texts);
}

public sealed class ConditionalRuleCatalog : IConditionalRuleCatalog
{
    public IReadOnlyList<ConditionalRuleKind> Kinds { get; } = Enum.GetValues<ConditionalRuleKind>();

    public IReadOnlyList<ConditionalStylePreset> StylePresets { get; } =
    [
        new("lightRedDarkRed", "#FFC7CE", "#9C0006"),
        new("yellowDarkYellow", "#FFEB9C", "#9C5700"),
        new("greenDarkGreen", "#C6EFCE", "#006100"),
        new("lightRed", "#FFC7CE", "#000000"),
        new("redText", null, "#C00000"),
        new("blueBold", "#DDEBF7", "#1F4E79", true),
        new("boldOnly", null, "#000000", true),
    ];

    public IReadOnlyList<ColorScalePreset> ScalePresets { get; } =
    [
        new("greenYellowRed", ["#F8696B", "#FFEB84", "#63BE7B"]),
        new("redYellowGreen", ["#63BE7B", "#FFEB84", "#F8696B"]),
        new("blueWhiteRed", ["#F8696B", "#FCFCFF", "#5A8AC6"]),
        new("redWhiteBlue", ["#5A8AC6", "#FCFCFF", "#F8696B"]),
        new("whiteGreen", ["#FCFCFF", "#63BE7B"]),
        new("whiteRed", ["#FCFCFF", "#F8696B"]),
        new("yellowGreen", ["#FFEF9C", "#63BE7B"]),
        new("whiteBlue", ["#FCFCFF", "#5A8AC6"]),
    ];

    public IReadOnlyList<string> BarColors { get; } = ["#638EC6", "#63C384", "#FF555A", "#FFB628", "#008AEF", "#D6007B"];

    public IReadOnlyList<ConditionOperator> Operators(ConditionalRuleKind kind) => kind switch
    {
        ConditionalRuleKind.CellValue =>
        [
            ConditionOperator.GreaterThan, ConditionOperator.GreaterOrEqual, ConditionOperator.LessThan, ConditionOperator.LessOrEqual,
            ConditionOperator.Equal, ConditionOperator.NotEqual, ConditionOperator.Between, ConditionOperator.NotBetween,
        ],
        ConditionalRuleKind.TopBottom => [ConditionOperator.TopItems, ConditionOperator.BottomItems, ConditionOperator.TopPercent, ConditionOperator.BottomPercent],
        ConditionalRuleKind.Average =>
        [
            ConditionOperator.AboveAverage, ConditionOperator.BelowAverage, ConditionOperator.AboveOrEqualAverage, ConditionOperator.BelowOrEqualAverage,
        ],
        ConditionalRuleKind.Duplicates => [ConditionOperator.Duplicate, ConditionOperator.Unique],
        ConditionalRuleKind.Text => [ConditionOperator.ContainsText, ConditionOperator.NotContainsText, ConditionOperator.BeginsWith, ConditionOperator.EndsWith],
        ConditionalRuleKind.Date => [ConditionOperator.DateIs],
        ConditionalRuleKind.Blanks => [ConditionOperator.IsBlank, ConditionOperator.IsNotBlank, ConditionOperator.IsError, ConditionOperator.IsNotError],
        _ => [],
    };

    public ConditionalRuleKind KindOf(ConditionalRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return rule.Visual switch
        {
            ConditionalVisual.DataBar => ConditionalRuleKind.DataBar,
            ConditionalVisual.ColorScale => ConditionalRuleKind.ColorScale,
            ConditionalVisual.IconSet => ConditionalRuleKind.IconSet,
            _ when rule.Formula is not null => ConditionalRuleKind.Formula,
            _ => Kinds.FirstOrDefault(kind => Operators(kind).Contains(rule.Operator), ConditionalRuleKind.CellValue),
        };
    }

    public bool IsStyled(ConditionalRuleKind kind) =>
        kind is not (ConditionalRuleKind.DataBar or ConditionalRuleKind.ColorScale or ConditionalRuleKind.IconSet);

    public ConditionalStylePreset? PresetOf(CellStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return StylePresets.FirstOrDefault(preset =>
            string.Equals(preset.BackgroundHex, style.BackgroundHex, StringComparison.OrdinalIgnoreCase)
            && string.Equals(preset.FontHex, style.Font.ColorHex, StringComparison.OrdinalIgnoreCase)
            && preset.Bold == style.Font.Bold);
    }

    public CellStyle StyleOf(ConditionalStylePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return new CellStyle
        {
            BackgroundHex = preset.BackgroundHex,
            Font = CellFont.Default with { ColorHex = preset.FontHex, Bold = preset.Bold },
        };
    }

    public ConditionalRule Default(ConditionalRuleKind kind, Addressing.CellRange range)
    {
        var style = StyleOf(StylePresets[0]);
        return kind switch
        {
            ConditionalRuleKind.DataBar => new ConditionalRule { Range = range, Visual = ConditionalVisual.DataBar },
            ConditionalRuleKind.ColorScale => new ConditionalRule { Range = range, Visual = ConditionalVisual.ColorScale, ScaleColors = ScalePresets[0].Colors },
            ConditionalRuleKind.IconSet => new ConditionalRule { Range = range, Visual = ConditionalVisual.IconSet, IconSetName = "3Arrows" },
            ConditionalRuleKind.Formula => new ConditionalRule { Range = range, Formula = string.Empty, Style = style },
            ConditionalRuleKind.TopBottom => new ConditionalRule { Range = range, Operator = ConditionOperator.TopItems, Operands = ["10"], Style = style },
            ConditionalRuleKind.Text => new ConditionalRule { Range = range, Operator = ConditionOperator.ContainsText, Operands = [string.Empty], Style = style },
            _ => new ConditionalRule { Range = range, Operator = Operators(kind) is [var first, ..] ? first : ConditionOperator.GreaterThan, Style = style },
        };
    }

    public string Describe(ConditionalRule rule, ITextCatalog texts)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(texts);
        var kind = KindOf(rule);
        var first = rule.Operands.Count > 0 ? rule.Operands[0] : string.Empty;
        var second = rule.Operands.Count > 1 ? rule.Operands[1] : string.Empty;
        var op = texts["condition.operator." + Camel(rule.Operator.ToString())];
        return kind switch
        {
            ConditionalRuleKind.DataBar => texts["conditional.kind.dataBar"],
            ConditionalRuleKind.ColorScale => texts.Format("conditional.describe.scale", rule.ScaleColors.Count),
            ConditionalRuleKind.IconSet => texts.Format("conditional.describe.icons", rule.IconCount),
            ConditionalRuleKind.Formula => texts.Format("conditional.describe.formula", rule.Formula ?? string.Empty),
            ConditionalRuleKind.Date => op + " " + texts["condition.period." + Camel(rule.DatePeriod.ToString())],
            ConditionalRuleKind.Average when rule.StandardDeviations > 0 => op + " (" + texts.Format("condition.deviations.count", rule.StandardDeviations) + ")",
            _ when rule.Operator is ConditionOperator.Between or ConditionOperator.NotBetween =>
                texts.Format("conditional.describe.between", op, first, second),
            _ when rule.Operator is ConditionOperator.ContainsText or ConditionOperator.NotContainsText or ConditionOperator.BeginsWith or ConditionOperator.EndsWith =>
                op + " «" + first + "»",
            _ when first.Length > 0 => op + " " + Number(first),
            _ => op,
        };
    }

    private string Number(string operand) =>
        double.TryParse(operand, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number.ToString("G", CultureInfo.CurrentCulture)
            : operand;

    private string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}

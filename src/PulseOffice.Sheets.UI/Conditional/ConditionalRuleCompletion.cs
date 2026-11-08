using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.UI.Conditional;

public interface IConditionalRuleCompletion
{
    ConditionalRule? Complete(ConditionalRuleDraft draft, IReadOnlyList<CellRange> ranges, CultureInfo culture, string language);
}

public sealed class ConditionalRuleCompletion(IConditionalRuleCatalog catalog, IFormulaLocalization formulas) : IConditionalRuleCompletion
{
    public ConditionalRule? Complete(ConditionalRuleDraft draft, IReadOnlyList<CellRange> ranges, CultureInfo culture, string language)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(culture);
        if (ranges.Count == 0) return null;

        var rule = draft.Rule with { Range = ranges[0], AdditionalRanges = [.. ranges.Skip(1)] };
        var kind = catalog.KindOf(rule);

        if (kind == ConditionalRuleKind.Formula)
        {
            var typed = draft.TypedFormula.Trim();
            if (typed.Length == 0) return null;
            return rule with { Formula = formulas.ToCanonical(typed.StartsWith('=') ? typed : "=" + typed, language), Operands = [] };
        }

        if (!catalog.IsStyled(kind)) return rule with { Formula = null };

        if (kind is ConditionalRuleKind.Date or ConditionalRuleKind.Average or ConditionalRuleKind.Duplicates or ConditionalRuleKind.Blanks)
            return rule with { Formula = null, Operands = [] };

        var needed = kind == ConditionalRuleKind.CellValue && rule.Operator is ConditionOperator.Between or ConditionOperator.NotBetween ? 2 : 1;
        var operands = draft.TypedOperands.Take(needed).Select(typed => kind == ConditionalRuleKind.Text ? typed : Operand(typed, culture)).ToList();
        if (operands.Count < needed || operands.Any(operand => operand.Length == 0)) return null;
        if (kind == ConditionalRuleKind.TopBottom)
        {
            var percent = rule.Operator is ConditionOperator.TopPercent or ConditionOperator.BottomPercent;
            if (!double.TryParse(operands[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var count) || !double.IsFinite(count) || count < 1 || (percent && count > 100)) return null;
            operands = [Math.Round(count).ToString(CultureInfo.InvariantCulture)];
        }
        return rule with { Formula = null, Operands = operands };
    }

    private string Operand(string typed, CultureInfo culture)
    {
        var text = typed.Trim();
        return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out var number)
            ? number.ToString(CultureInfo.InvariantCulture)
            : text;
    }
}

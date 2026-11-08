using System.Globalization;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Formats.Odf;

internal sealed record ParsedCondition(ConditionOperator Operator, IReadOnlyList<string> Operands, string? Formula);

internal sealed class OdfConditionLiterals
{
    public string Unquote(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
            ? trimmed[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal)
            : trimmed;
    }

    public IReadOnlyList<string> Arguments(string text)
    {
        var arguments = new List<string>();
        var start = 0;
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '"') quoted = !quoted;
            else if (text[index] == ',' && !quoted)
            {
                arguments.Add(Unquote(text[start..index]));
                start = index + 1;
            }
        }
        arguments.Add(Unquote(text[start..]));
        return arguments;
    }

    public string Literal(string operand) =>
        double.TryParse(operand, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            ? operand
            : "\"" + operand.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}

internal abstract class ConditionForm
{
    protected OdfConditionLiterals Text { get; } = new();

    public abstract bool Writes(ConditionalRule rule);

    public abstract string Write(ConditionalRule rule);

    public abstract ParsedCondition? Read(string value);

    protected IReadOnlyList<string> Literals(ConditionalRule rule)
    {
        var operands = rule.Operands.Select(Text.Literal).ToList();
        var first = operands.Count > 0 ? operands[0] : "0";
        return [first, operands.Count > 1 ? operands[1] : first];
    }
}

internal sealed class FormulaConditionForm : ConditionForm
{
    private const string Name = "formula-is";

    public override bool Writes(ConditionalRule rule) => rule.Formula is { Length: > 0 };

    public override string Write(ConditionalRule rule) => $"{Name}({new OdfFormulaSyntax().ToOdfExpression(rule.Formula!)})";

    public override ParsedCondition? Read(string value) =>
        value.StartsWith(Name + "(", StringComparison.Ordinal) && value.EndsWith(')')
            ? new ParsedCondition(ConditionOperator.GreaterThan, [], "=" + new OdfFormulaSyntax().ToInternalExpression(value[(Name.Length + 1)..^1]))
            : null;
}

internal sealed class FunctionConditionForm(ConditionOperator op, string name, int arity) : ConditionForm
{
    public override bool Writes(ConditionalRule rule) => rule.Operator == op;

    public override string Write(ConditionalRule rule) =>
        $"{name}({string.Join(",", Literals(rule).Take(arity))})";

    public override ParsedCondition? Read(string value) =>
        value.StartsWith(name + "(", StringComparison.Ordinal) && value.EndsWith(')')
            ? new ParsedCondition(op, Text.Arguments(value[(name.Length + 1)..^1]), null)
            : null;
}

internal sealed class KeywordConditionForm(ConditionOperator op, string keyword) : ConditionForm
{
    public override bool Writes(ConditionalRule rule) => rule.Operator == op;

    public override string Write(ConditionalRule rule) => keyword;

    public override ParsedCondition? Read(string value) => value == keyword ? new ParsedCondition(op, [], null) : null;
}

internal sealed class ComparisonConditionForm(ConditionOperator op, string symbol) : ConditionForm
{
    public override bool Writes(ConditionalRule rule) => rule.Operator == op;

    public override string Write(ConditionalRule rule) => symbol + Literals(rule)[0];

    public override ParsedCondition? Read(string value) =>
        value.StartsWith(symbol, StringComparison.Ordinal) ? new ParsedCondition(op, [Text.Unquote(value[symbol.Length..])], null) : null;
}

internal sealed class OdfConditionSyntax
{
    private readonly IReadOnlyList<ConditionForm> _forms =
    [
        new FormulaConditionForm(),
        new FunctionConditionForm(ConditionOperator.Between, "between", 2),
        new FunctionConditionForm(ConditionOperator.NotBetween, "not-between", 2),
        new FunctionConditionForm(ConditionOperator.ContainsText, "contains-text", 1),
        new FunctionConditionForm(ConditionOperator.NotContainsText, "not-contains-text", 1),
        new FunctionConditionForm(ConditionOperator.BeginsWith, "begins-with", 1),
        new FunctionConditionForm(ConditionOperator.EndsWith, "ends-with", 1),
        new KeywordConditionForm(ConditionOperator.IsBlank, "is-empty"),
        new KeywordConditionForm(ConditionOperator.IsNotBlank, "is-not-empty"),
        new KeywordConditionForm(ConditionOperator.IsError, "is-error"),
        new KeywordConditionForm(ConditionOperator.IsNotError, "is-no-error"),
        new KeywordConditionForm(ConditionOperator.Duplicate, "duplicate"),
        new KeywordConditionForm(ConditionOperator.Unique, "unique"),
        new KeywordConditionForm(ConditionOperator.AboveAverage, "above-average"),
        new KeywordConditionForm(ConditionOperator.BelowAverage, "below-average"),
        new KeywordConditionForm(ConditionOperator.AboveOrEqualAverage, "above-equal-average"),
        new KeywordConditionForm(ConditionOperator.BelowOrEqualAverage, "below-equal-average"),
        new FunctionConditionForm(ConditionOperator.TopItems, "top-elements", 1),
        new FunctionConditionForm(ConditionOperator.BottomItems, "bottom-elements", 1),
        new FunctionConditionForm(ConditionOperator.TopPercent, "top-percent", 1),
        new FunctionConditionForm(ConditionOperator.BottomPercent, "bottom-percent", 1),
        new ComparisonConditionForm(ConditionOperator.GreaterOrEqual, ">="),
        new ComparisonConditionForm(ConditionOperator.LessOrEqual, "<="),
        new ComparisonConditionForm(ConditionOperator.NotEqual, "!="),
        new ComparisonConditionForm(ConditionOperator.Equal, "="),
        new ComparisonConditionForm(ConditionOperator.GreaterThan, ">"),
        new ComparisonConditionForm(ConditionOperator.LessThan, "<"),
    ];

    public string Write(ConditionalRule rule) =>
        _forms.FirstOrDefault(form => form.Writes(rule))?.Write(rule) ?? "is-no-error";

    public ParsedCondition Read(string value) =>
        _forms.Select(form => form.Read(value)).FirstOrDefault(parsed => parsed is not null)
        ?? new ParsedCondition(ConditionOperator.GreaterThan, [_literals.Unquote(value)], null);

    private readonly OdfConditionLiterals _literals = new();
}

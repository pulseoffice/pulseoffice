using System.Globalization;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxComparisonOperands(XlsxFormulaSyntax formulas)
{
    private readonly PulseOffice.Sheets.Engine.Operations.ConditionalComparisonFormula _comparison = new();

    public ConditionalRule Apply(ConditionalRule basis, ConditionOperator op, IReadOnlyList<string> expressions)
    {
        if (expressions.Count == 0) return basis with { Operator = op };

        var literals = expressions.Select(Literal).ToList();
        if (literals.All(literal => literal is not null)) return basis with { Operator = op, Operands = [.. literals.OfType<string>()] };

        var expression = _comparison.Expression(op, basis.Anchor, expressions);
        return basis with { Formula = formulas.ToInternal(expression) };
    }

    public string? Literal(string expression)
    {
        if (double.TryParse(expression, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return expression;
        if (expression.Length >= 2 && expression[0] == '"' && expression[^1] == '"' && !expression[1..^1].Replace("\"\"", string.Empty, StringComparison.Ordinal).Contains('"'))
            return expression[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
        return null;
    }
}

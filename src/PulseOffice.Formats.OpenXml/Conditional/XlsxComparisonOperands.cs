using System.Globalization;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxComparisonOperands(XlsxFormulaSyntax formulas)
{
    public ConditionalRule Apply(ConditionalRule basis, ConditionOperator op, IReadOnlyList<string> expressions)
    {
        if (expressions.Count == 0) return basis with { Operator = op };

        var literals = expressions.Select(Literal).ToList();
        if (literals.All(literal => literal is not null)) return basis with { Operator = op, Operands = [.. literals.OfType<string>()] };

        var anchor = basis.Anchor.ToA1();
        var first = expressions[0];
        var second = expressions.Count > 1 ? expressions[1] : first;
        var expression = op switch
        {
            ConditionOperator.Between => $"AND({anchor}>={first},{anchor}<={second})",
            ConditionOperator.NotBetween => $"OR({anchor}<{first},{anchor}>{second})",
            ConditionOperator.NotEqual => $"{anchor}<>{first}",
            ConditionOperator.GreaterThan => $"{anchor}>{first}",
            ConditionOperator.GreaterOrEqual => $"{anchor}>={first}",
            ConditionOperator.LessThan => $"{anchor}<{first}",
            ConditionOperator.LessOrEqual => $"{anchor}<={first}",
            _ => $"{anchor}={first}",
        };
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

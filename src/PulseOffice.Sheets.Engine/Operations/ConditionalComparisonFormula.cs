using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed class ConditionalComparisonFormula
{
    public string Expression(ConditionOperator op, CellAddress anchor, IReadOnlyList<string> expressions)
    {
        ArgumentNullException.ThrowIfNull(expressions);
        var cell = anchor.ToA1();
        var first = expressions.Count > 0 ? expressions[0] : "0";
        var second = expressions.Count > 1 ? expressions[1] : first;
        return op switch
        {
            ConditionOperator.Between => $"AND({cell}>={first},{cell}<={second})",
            ConditionOperator.NotBetween => $"OR({cell}<{first},{cell}>{second})",
            ConditionOperator.NotEqual => $"{cell}<>{first}",
            ConditionOperator.GreaterThan => $"{cell}>{first}",
            ConditionOperator.GreaterOrEqual => $"{cell}>={first}",
            ConditionOperator.LessThan => $"{cell}<{first}",
            ConditionOperator.LessOrEqual => $"{cell}<={first}",
            _ => $"{cell}={first}",
        };
    }
}

using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI.Analysis;

public sealed record SolverConstraintRow(string Cell, ConstraintOperator Operator, string Value)
{
    public bool NeedsValue => Operator is ConstraintOperator.LessOrEqual or ConstraintOperator.Equal or ConstraintOperator.GreaterOrEqual;

    public bool IsBlank => Cell.Trim().Length == 0 && (!NeedsValue || Value.Trim().Length == 0);
}

public interface ISolverConstraintRows
{
    bool TryBuild(SolverConstraintRow row, int sheetIndex, out SolverConstraint constraint);

    SolverConstraintRows.Outcome Build(IEnumerable<SolverConstraintRow> rows, int sheetIndex);
}

public sealed class SolverConstraintRows(CultureInfo culture) : ISolverConstraintRows
{
    public sealed record Outcome(IReadOnlyList<SolverConstraint> Constraints, IReadOnlyList<int> Invalid)
    {
        public bool Valid => Invalid.Count == 0;
    }

    public bool TryBuild(SolverConstraintRow row, int sheetIndex, out SolverConstraint constraint)
    {
        ArgumentNullException.ThrowIfNull(row);
        constraint = null!;

        if (!CellAddress.TryParseA1(row.Cell.Trim(), out var address)) return false;

        var value = 0d;
        if (row.NeedsValue && !TryNumber(row.Value.Trim(), out value)) return false;

        constraint = new SolverConstraint(new SheetCell(sheetIndex, address), row.Operator, value);
        return true;
    }

    public Outcome Build(IEnumerable<SolverConstraintRow> rows, int sheetIndex)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var constraints = new List<SolverConstraint>();
        var invalid = new List<int>();
        var index = 0;

        foreach (var row in rows)
        {
            if (!row.IsBlank)
            {
                if (TryBuild(row, sheetIndex, out var constraint)) constraints.Add(constraint);
                else invalid.Add(index);
            }

            index++;
        }

        return new Outcome(constraints, invalid);
    }

    private bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, culture, out value)
        || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}

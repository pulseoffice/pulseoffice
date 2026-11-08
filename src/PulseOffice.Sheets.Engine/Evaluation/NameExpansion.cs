using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Parsing;

namespace PulseOffice.Sheets.Engine.Evaluation;

public interface INameExpansion
{
    FormulaNode Expand(string expression, CellAddress usedAt);
}

public sealed class RelativeNameExpansion : INameExpansion
{
    public FormulaNode Expand(string expression, CellAddress usedAt)
    {
        var parsed = new FormulaParser().Parse(expression);
        return usedAt == CellAddress.First ? parsed : parsed.Accept(new WrappingOffsetRewriter(usedAt.Row, usedAt.Column));
    }
}

public interface INameAnchoring
{
    string FromCell(string expression, CellAddress activeCell);

    string ToCell(string expression, CellAddress activeCell);
}

public sealed class ActiveCellNameAnchoring : INameAnchoring
{
    public string FromCell(string expression, CellAddress activeCell) =>
        Shift(expression, -activeCell.Row, -activeCell.Column);

    public string ToCell(string expression, CellAddress activeCell) =>
        Shift(expression, activeCell.Row, activeCell.Column);

    private static string Shift(string expression, int rows, int columns)
    {
        if (rows == 0 && columns == 0) return expression;
        var rewritten = new WrappingOffsetRewriter(rows, columns).Rewrite(expression);
        return rewritten is null ? expression : rewritten[1..];
    }
}

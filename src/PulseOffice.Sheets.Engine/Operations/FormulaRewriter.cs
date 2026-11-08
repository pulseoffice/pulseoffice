using System.Text;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public abstract class FormulaRewriter : FormulaNodeVisitor<FormulaNode>
{
    private readonly BinaryOperators _operators = new();

    public string? Rewrite(string formula)
    {
        ArgumentNullException.ThrowIfNull(formula);

        try
        {
            var builder = new StringBuilder("=");
            new FormulaParser().Parse(formula).Accept(this).Accept(new CanonicalFormulaPrinter(builder, _operators));
            return builder.ToString();
        }
        catch (FormulaSyntaxException)
        {
            return null;
        }
    }

    public abstract override FormulaNode VisitReference(ReferenceNode node);

    public abstract override FormulaNode VisitRange(RangeNode node);

    public override FormulaNode VisitUnary(UnaryNode node) => node with { Operand = node.Operand.Accept(this) };

    public override FormulaNode VisitBinary(BinaryNode node) => node with { Left = node.Left.Accept(this), Right = node.Right.Accept(this) };

    public override FormulaNode VisitFunction(FunctionNode node) => node with { Arguments = [.. node.Arguments.Select(argument => argument.Accept(this))] };

    public override FormulaNode VisitInvocation(InvocationNode node) =>
        node with { Callee = node.Callee.Accept(this), Arguments = [.. node.Arguments.Select(argument => argument.Accept(this))] };

    public override FormulaNode VisitArray(ArrayNode node) =>
        node with { Rows = [.. node.Rows.Select(row => (IReadOnlyList<FormulaNode>)[.. row.Select(cell => cell.Accept(this))])] };

    protected override FormulaNode Otherwise(FormulaNode node) => node;

    protected FormulaNode Broken() => new LiteralNode(CellValue.FromError(CellError.Reference));
}

public class OffsetRewriter(int rowDelta, int columnDelta) : FormulaRewriter
{
    public override FormulaNode VisitReference(ReferenceNode node)
    {
        var moved = Move(node.Address, node.AbsoluteRow, node.AbsoluteColumn);

        return moved is null
            ? Broken()
            : node with { Address = moved.Value };
    }

    public override FormulaNode VisitSpillReference(SpillReferenceNode node)
    {
        var moved = Move(node.Anchor, node.AbsoluteRow, node.AbsoluteColumn);
        return moved is null ? Broken() : node with { Anchor = moved.Value };
    }

    public override FormulaNode VisitRange(RangeNode node)
    {
        var start = Move(node.Range.Start, node.StartAbsoluteRow, node.StartAbsoluteColumn);
        var end = Move(node.Range.End, node.EndAbsoluteRow, node.EndAbsoluteColumn);

        return start is null || end is null
            ? Broken()
            : node with { Range = new CellRange(start.Value, end.Value) };
    }

    private CellAddress? Move(CellAddress address, bool absoluteRow, bool absoluteColumn)
    {
        var row = absoluteRow ? address.Row : address.Row + rowDelta;
        var column = absoluteColumn ? address.Column : address.Column + columnDelta;

        return Place(row, column);
    }

    protected virtual CellAddress? Place(int row, int column) =>
        row < 0 || column < 0 || row >= CellAddress.MaxRows || column >= CellAddress.MaxColumns ? null : new CellAddress(row, column);
}

public sealed class WrappingOffsetRewriter(int rowDelta, int columnDelta) : OffsetRewriter(rowDelta, columnDelta)
{
    protected override CellAddress? Place(int row, int column) =>
        new(Wrap(row, CellAddress.MaxRows), Wrap(column, CellAddress.MaxColumns));

    private static int Wrap(int value, int size) => ((value % size) + size) % size;
}

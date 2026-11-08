using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Parsing;

public abstract record FormulaNode
{
    public abstract TResult Accept<TResult>(IFormulaNodeVisitor<TResult> visitor);
}

public sealed record LiteralNode(CellValue Value) : FormulaNode
{
    public override TResult Accept<TResult>(IFormulaNodeVisitor<TResult> visitor) => visitor.VisitLiteral(this);
}

public sealed record ReferenceNode(
    string? SheetName,
    CellAddress Address,
    bool AbsoluteColumn,
    bool AbsoluteRow) : FormulaNode
{
    public override TResult Accept<TResult>(IFormulaNodeVisitor<TResult> visitor) => visitor.VisitReference(this);
}

public sealed record RangeNode(
    string? SheetName,
    CellRange Range,
    bool StartAbsoluteColumn, bool StartAbsoluteRow,
    bool EndAbsoluteColumn, bool EndAbsoluteRow) : FormulaNode
{
    public string? LastSheetName { get; init; }

    public bool SpansSheets => SheetName is not null && LastSheetName is not null;

    public override TResult Accept<TResult>(IFormulaNodeVisitor<TResult> visitor) => visitor.VisitRange(this);
}

public sealed record SpillReferenceNode(string? SheetName, CellAddress Anchor) : FormulaNode
{
    public bool AbsoluteColumn { get; init; }

    public bool AbsoluteRow { get; init; }

    public override TResult Accept<TResult>(IFormulaNodeVisitor<TResult> visitor) => visitor.VisitSpillReference(this);
}

public sealed record NameNode(string Name) : FormulaNode
{
    public override TResult Accept<TResult>(IFormulaNodeVisitor<TResult> visitor) => visitor.VisitName(this);
}

public enum UnaryOperator { Plus, Minus, Percent, Single }

public enum BinaryOperator
{
    Add, Subtract, Multiply, Divide, Power, Concat,
    Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual,
    ReferenceConcat,
}

public sealed record UnaryNode(UnaryOperator Operator, FormulaNode Operand) : FormulaNode
{
    public override TResult Accept<TResult>(IFormulaNodeVisitor<TResult> visitor) => visitor.VisitUnary(this);
}

public sealed record BinaryNode(BinaryOperator Operator, FormulaNode Left, FormulaNode Right) : FormulaNode
{
    public override TResult Accept<TResult>(IFormulaNodeVisitor<TResult> visitor) => visitor.VisitBinary(this);
}

public sealed record FunctionNode(string Name, IReadOnlyList<FormulaNode> Arguments) : FormulaNode
{
    public override TResult Accept<TResult>(IFormulaNodeVisitor<TResult> visitor) => visitor.VisitFunction(this);
}

public sealed record InvocationNode(FormulaNode Callee, IReadOnlyList<FormulaNode> Arguments) : FormulaNode
{
    public override TResult Accept<TResult>(IFormulaNodeVisitor<TResult> visitor) => visitor.VisitInvocation(this);
}

public sealed record ArrayNode(IReadOnlyList<IReadOnlyList<FormulaNode>> Rows) : FormulaNode
{
    public override TResult Accept<TResult>(IFormulaNodeVisitor<TResult> visitor) => visitor.VisitArray(this);
}

public sealed record MissingArgumentNode : FormulaNode
{
    public override TResult Accept<TResult>(IFormulaNodeVisitor<TResult> visitor) => visitor.VisitMissingArgument(this);
}

public interface IFormulaNodeVisitor<out TResult>
{
    TResult VisitLiteral(LiteralNode node);

    TResult VisitReference(ReferenceNode node);

    TResult VisitRange(RangeNode node);

    TResult VisitSpillReference(SpillReferenceNode node);

    TResult VisitName(NameNode node);

    TResult VisitUnary(UnaryNode node);

    TResult VisitBinary(BinaryNode node);

    TResult VisitFunction(FunctionNode node);

    TResult VisitArray(ArrayNode node);

    TResult VisitInvocation(InvocationNode node);

    TResult VisitMissingArgument(MissingArgumentNode node);
}

public abstract class FormulaNodeVisitor<TResult> : IFormulaNodeVisitor<TResult>
{
    public virtual TResult VisitLiteral(LiteralNode node) => Otherwise(node);

    public virtual TResult VisitReference(ReferenceNode node) => Otherwise(node);

    public virtual TResult VisitRange(RangeNode node) => Otherwise(node);

    public virtual TResult VisitSpillReference(SpillReferenceNode node) => Otherwise(node);

    public virtual TResult VisitName(NameNode node) => Otherwise(node);

    public virtual TResult VisitUnary(UnaryNode node) => Otherwise(node);

    public virtual TResult VisitBinary(BinaryNode node) => Otherwise(node);

    public virtual TResult VisitFunction(FunctionNode node) => Otherwise(node);

    public virtual TResult VisitArray(ArrayNode node) => Otherwise(node);

    public virtual TResult VisitInvocation(InvocationNode node) => Otherwise(node);

    public virtual TResult VisitMissingArgument(MissingArgumentNode node) => Otherwise(node);

    protected abstract TResult Otherwise(FormulaNode node);
}

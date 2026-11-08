using System.Globalization;
using System.Text;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Parsing;

public sealed class BinaryOperators
{
    private readonly IReadOnlyDictionary<BinaryOperator, (string Symbol, int Precedence)> _table = new Dictionary<BinaryOperator, (string, int)>
    {
        [BinaryOperator.Equal] = ("=", 1), [BinaryOperator.NotEqual] = ("<>", 1),
        [BinaryOperator.Less] = ("<", 1), [BinaryOperator.LessOrEqual] = ("<=", 1),
        [BinaryOperator.Greater] = (">", 1), [BinaryOperator.GreaterOrEqual] = (">=", 1),
        [BinaryOperator.Concat] = ("&", 2),
        [BinaryOperator.Add] = ("+", 3), [BinaryOperator.Subtract] = ("-", 3),
        [BinaryOperator.Multiply] = ("*", 4), [BinaryOperator.Divide] = ("/", 4),
        [BinaryOperator.Power] = ("^", 5),
        [BinaryOperator.ReferenceConcat] = ("~", 7),
    };

    public string Symbol(BinaryOperator op) => _table[op].Symbol;

    public bool NeedsParentheses(BinaryOperator child, BinaryOperator parent, bool right)
    {
        var inner = _table[child].Precedence;
        var outer = _table[parent].Precedence;
        if (inner != outer) return inner < outer;
        return right && parent is not (BinaryOperator.Add or BinaryOperator.Multiply or BinaryOperator.Concat or BinaryOperator.ReferenceConcat);
    }
}

public abstract class FormulaPrinter : FormulaNodeVisitor<bool>
{
    protected FormulaPrinter(StringBuilder builder, BinaryOperators operators)
    {
        Builder = builder;
        Operators = operators;
    }

    protected StringBuilder Builder { get; }

    protected BinaryOperators Operators { get; }

    protected abstract string Separator { get; }

    protected virtual string ArrayColumnSeparator => Separator;

    protected virtual string ArrayRowSeparator => "|";

    public override bool VisitReference(ReferenceNode node)
    {
        Sheet(node.SheetName);
        Builder.Append(node.Address.ToA1(node.AbsoluteColumn, node.AbsoluteRow));
        return true;
    }

    protected void AppendRange(RangeNode node)
    {
        var range = node.Range;
        if (range.IsWholeColumns && !range.IsWholeRows)
        {
            Builder.Append(node.StartAbsoluteColumn ? "$" : string.Empty).Append(CellAddress.ColumnName(range.Start.Column))
                .Append(':')
                .Append(node.EndAbsoluteColumn ? "$" : string.Empty).Append(CellAddress.ColumnName(range.End.Column));
            return;
        }
        if (range.IsWholeRows && !range.IsWholeColumns)
        {
            Builder.Append(node.StartAbsoluteRow ? "$" : string.Empty).Append((range.Start.Row + 1).ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(node.EndAbsoluteRow ? "$" : string.Empty).Append((range.End.Row + 1).ToString(CultureInfo.InvariantCulture));
            return;
        }
        Builder.Append(range.Start.ToA1(node.StartAbsoluteColumn, node.StartAbsoluteRow)).Append(':');
        if (node.SpansSheets) Sheet(node.LastSheetName);
        Builder.Append(range.End.ToA1(node.EndAbsoluteColumn, node.EndAbsoluteRow));
    }

    public override bool VisitSpillReference(SpillReferenceNode node)
    {
        Sheet(node.SheetName);
        Builder.Append(node.Anchor.ToA1(node.AbsoluteColumn, node.AbsoluteRow)).Append('#');
        return true;
    }

    public override bool VisitName(NameNode node)
    {
        Builder.Append(node.Name);
        return true;
    }

    public override bool VisitUnary(UnaryNode node)
    {
        if (node.Operator == UnaryOperator.Percent)
        {
            Grouped(node.Operand);
            Builder.Append('%');
        }
        else
        {
            Builder.Append(node.Operator switch { UnaryOperator.Minus => '-', UnaryOperator.Single => '@', _ => '+' });
            Grouped(node.Operand);
        }
        return true;
    }

    protected virtual void Grouped(FormulaNode operand)
    {
        var needed = operand is BinaryNode;
        if (needed) Builder.Append('(');
        Print(operand);
        if (needed) Builder.Append(')');
    }

    public override bool VisitFunction(FunctionNode node)
    {
        Builder.Append(FunctionName(node.Name)).Append('(');
        Joined(node.Arguments, Separator);
        Builder.Append(')');
        return true;
    }

    public override bool VisitInvocation(InvocationNode node)
    {
        Print(node.Callee);
        Builder.Append('(');
        Joined(node.Arguments, Separator);
        Builder.Append(')');
        return true;
    }

    public override bool VisitArray(ArrayNode node)
    {
        Builder.Append('{');
        for (var row = 0; row < node.Rows.Count; row++)
        {
            if (row > 0) Builder.Append(ArrayRowSeparator);
            Joined(node.Rows[row], ArrayColumnSeparator);
        }
        Builder.Append('}');
        return true;
    }

    protected override bool Otherwise(FormulaNode node) => true;

    protected virtual string FunctionName(string name) => name;

    protected void Print(FormulaNode node) => node.Accept(this);

    private readonly SheetNameQuoting _quoting = new();

    protected void Sheet(string? sheet)
    {
        if (sheet is not null) Builder.Append(_quoting.Quoted(sheet)).Append('.');
    }

    public override bool VisitBinary(BinaryNode node)
    {
        Operand(node.Left, node.Operator, false);
        Builder.Append(Operators.Symbol(node.Operator));
        Operand(node.Right, node.Operator, true);
        return true;
    }

    private void Operand(FormulaNode operand, BinaryOperator parent, bool right)
    {
        var needed = operand is BinaryNode child && Operators.NeedsParentheses(child.Operator, parent, right);
        if (needed) Builder.Append('(');
        Print(operand);
        if (needed) Builder.Append(')');
    }

    private void Joined(IReadOnlyList<FormulaNode> nodes, string separator)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            if (index > 0) Builder.Append(separator);
            Print(nodes[index]);
        }
    }
}

public sealed class CanonicalFormulaPrinter(StringBuilder builder, BinaryOperators operators) : FormulaPrinter(builder, operators)
{
    protected override string Separator => ";";

    public override bool VisitLiteral(LiteralNode node)
    {
        Builder.Append(node.Value.Kind == ValueKind.Text
            ? $"\"{node.Value.AsText().Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : node.Value.ToString());
        return true;
    }

    public override bool VisitRange(RangeNode node)
    {
        Sheet(node.SheetName);
        AppendRange(node);
        return true;
    }

}

public sealed class LocalizedFormulaPrinter(StringBuilder builder, BinaryOperators operators, FormulaPunctuation punctuation, Func<string, string> functionName)
    : FormulaPrinter(builder, operators)
{
    protected override string Separator => punctuation.Argument.ToString();

    protected override string ArrayColumnSeparator => punctuation.ArrayColumn.ToString();

    protected override string ArrayRowSeparator => punctuation.ArrayRow.ToString();

    public override bool VisitLiteral(LiteralNode node)
    {
        var value = node.Value;
        switch (value.Kind)
        {
            case ValueKind.Text:
                Builder.Append('"').Append(value.AsText().Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
                break;
            case ValueKind.Boolean:
                Builder.Append(value.AsBoolean() ? "TRUE" : "FALSE");
                break;
            case ValueKind.Error:
                Builder.Append(value.ErrorText);
                break;
            default:
                Builder.Append(value.AsNumber().ToString("R", CultureInfo.InvariantCulture).Replace('.', punctuation.Decimal));
                break;
        }
        return true;
    }

    public override bool VisitRange(RangeNode node)
    {
        Sheet(node.SheetName);
        AppendRange(node);
        return true;
    }

    protected override string FunctionName(string name) => functionName(name);
}

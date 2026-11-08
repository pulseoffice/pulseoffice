using System.Globalization;
using System.Text;

namespace PulseOffice.Document.Tables;

public interface ITableFormulaContext
{
    int Row { get; }

    int Column { get; }

    int RowCount { get; }

    int ColumnCount { get; }

    double? Value(int row, int column);
}

public abstract class FormulaNode
{
    public abstract double Evaluate(ITableFormulaContext context);

    public abstract T Accept<T>(IFormulaNodeVisitor<T> visitor);

    public virtual IEnumerable<double> Values(ITableFormulaContext context) => [Evaluate(context)];
}

public interface IFormulaNodeVisitor<out T>
{
    T VisitNumber(NumberNode node);

    T VisitReference(ReferenceNode node);

    T VisitRange(RangeNode node);

    T VisitDirection(DirectionNode node);

    T VisitUnary(UnaryNode node);

    T VisitBinary(BinaryNode node);

    T VisitFunction(FunctionNode node);
}

public sealed class NumberNode(double value) : FormulaNode
{
    public double Value { get; } = value;

    public override double Evaluate(ITableFormulaContext context) => Value;

    public override T Accept<T>(IFormulaNodeVisitor<T> visitor) => visitor.VisitNumber(this);
}

public sealed class ReferenceNode(int row, int column) : FormulaNode
{
    public int Row { get; } = row;

    public int Column { get; } = column;

    public override double Evaluate(ITableFormulaContext context) => context.Value(Row, Column) ?? 0;

    public override IEnumerable<double> Values(ITableFormulaContext context) =>
        context.Value(Row, Column) is { } value ? [value] : [];

    public override T Accept<T>(IFormulaNodeVisitor<T> visitor) => visitor.VisitReference(this);
}

public sealed class RangeNode(TableCellRange range) : FormulaNode
{
    public TableCellRange Range { get; } = range.Normalized;

    public override double Evaluate(ITableFormulaContext context) => Values(context).Sum();

    public override IEnumerable<double> Values(ITableFormulaContext context)
    {
        var bottom = Math.Min(Range.Bottom, context.RowCount - 1);
        var right = Math.Min(Range.Right, context.ColumnCount - 1);
        for (var row = Math.Max(0, Range.Top); row <= bottom; row++)
            for (var column = Math.Max(0, Range.Left); column <= right; column++)
                if (context.Value(row, column) is { } value) yield return value;
    }

    public override T Accept<T>(IFormulaNodeVisitor<T> visitor) => visitor.VisitRange(this);
}

public enum FormulaDirection
{
    Above,
    Below,
    Left,
    Right,
}

public sealed class DirectionNode(FormulaDirection direction) : FormulaNode
{
    public FormulaDirection Direction { get; } = direction;

    public TableCellRange? Resolve(ITableFormulaContext context) => Direction switch
    {
        FormulaDirection.Above when context.Row > 0 => new TableCellRange(0, context.Column, context.Row - 1, context.Column),
        FormulaDirection.Below when context.Row + 1 < context.RowCount => new TableCellRange(context.Row + 1, context.Column, context.RowCount - 1, context.Column),
        FormulaDirection.Left when context.Column > 0 => new TableCellRange(context.Row, 0, context.Row, context.Column - 1),
        FormulaDirection.Right when context.Column + 1 < context.ColumnCount => new TableCellRange(context.Row, context.Column + 1, context.Row, context.ColumnCount - 1),
        _ => null,
    };

    public override double Evaluate(ITableFormulaContext context) => Values(context).Sum();

    public override IEnumerable<double> Values(ITableFormulaContext context) =>
        Resolve(context) is { } range ? new RangeNode(range).Values(context) : [];

    public override T Accept<T>(IFormulaNodeVisitor<T> visitor) => visitor.VisitDirection(this);
}

public sealed class UnaryNode(char sign, FormulaNode operand) : FormulaNode
{
    public char Sign { get; } = sign;

    public FormulaNode Operand { get; } = operand;

    public override double Evaluate(ITableFormulaContext context) => Sign == '-' ? -Operand.Evaluate(context) : Operand.Evaluate(context);

    public override T Accept<T>(IFormulaNodeVisitor<T> visitor) => visitor.VisitUnary(this);
}

public sealed class BinaryNode(char operation, FormulaNode left, FormulaNode right) : FormulaNode
{
    public char Operation { get; } = operation;

    public FormulaNode Left { get; } = left;

    public FormulaNode Right { get; } = right;

    public override double Evaluate(ITableFormulaContext context)
    {
        var a = Left.Evaluate(context);
        var b = Right.Evaluate(context);
        return Operation switch
        {
            '+' => a + b,
            '-' => a - b,
            '*' => a * b,
            '/' => b == 0 ? double.NaN : a / b,
            _ => a,
        };
    }

    public override T Accept<T>(IFormulaNodeVisitor<T> visitor) => visitor.VisitBinary(this);
}

public enum FormulaFunction
{
    Sum,
    Average,
    Min,
    Max,
    Count,
    Product,
}

public sealed class FunctionNode(FormulaFunction function, IReadOnlyList<FormulaNode> arguments) : FormulaNode
{
    public FormulaFunction Function { get; } = function;

    public IReadOnlyList<FormulaNode> Arguments { get; } = arguments;

    public override double Evaluate(ITableFormulaContext context)
    {
        var values = Arguments.SelectMany(argument => argument.Values(context)).ToList();
        return Function switch
        {
            FormulaFunction.Sum => values.Sum(),
            FormulaFunction.Average => values.Count == 0 ? 0 : values.Average(),
            FormulaFunction.Min => values.Count == 0 ? 0 : values.Min(),
            FormulaFunction.Max => values.Count == 0 ? 0 : values.Max(),
            FormulaFunction.Count => values.Count,
            _ => values.Count == 0 ? 0 : values.Aggregate(1.0, (product, value) => product * value),
        };
    }

    public override T Accept<T>(IFormulaNodeVisitor<T> visitor) => visitor.VisitFunction(this);
}

public sealed class TableFormulaException(string message) : FormatException(message);

public interface ITableFormulaParser
{
    FormulaNode Parse(string formula);

    bool TryParse(string formula, out FormulaNode? node);
}

public sealed class TableFormulaParser : ITableFormulaParser
{
    private readonly Dictionary<string, FormulaFunction> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SUM"] = FormulaFunction.Sum,
        ["AVERAGE"] = FormulaFunction.Average,
        ["MEAN"] = FormulaFunction.Average,
        ["MIN"] = FormulaFunction.Min,
        ["MAX"] = FormulaFunction.Max,
        ["COUNT"] = FormulaFunction.Count,
        ["PRODUCT"] = FormulaFunction.Product,
    };

    private readonly Dictionary<string, FormulaDirection> Directions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ABOVE"] = FormulaDirection.Above,
        ["BELOW"] = FormulaDirection.Below,
        ["LEFT"] = FormulaDirection.Left,
        ["RIGHT"] = FormulaDirection.Right,
    };

    public bool TryParse(string formula, out FormulaNode? node)
    {
        try
        {
            node = Parse(formula);
            return true;
        }
        catch (TableFormulaException)
        {
            node = null;
            return false;
        }
    }

    public FormulaNode Parse(string formula)
    {
        var text = formula.Trim();
        foreach (var prefix in new[] { "ooow:", "of:", "=" })
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) text = text[prefix.Length..].Trim();
        var reader = new Reader(Tokenize(text), this);
        var node = reader.Expression();
        if (!reader.AtEnd) throw new TableFormulaException("Unexpected token in table formula");
        return node;
    }

    private List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var index = 0;
        while (index < text.Length)
        {
            var c = text[index];
            if (char.IsWhiteSpace(c))
            {
                index++;
                continue;
            }
            if (c == '<')
            {
                var close = text.IndexOf('>', index);
                if (close < 0) throw new TableFormulaException("Unclosed cell reference");
                tokens.Add(text[(index + 1)..close].Trim());
                index = close + 1;
                continue;
            }
            if ("+-*/(),;|".Contains(c))
            {
                tokens.Add(c.ToString());
                index++;
                continue;
            }
            var start = index;
            while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] is '.' or ':' or '$')) index++;
            if (start == index) throw new TableFormulaException("Unexpected character in table formula");
            tokens.Add(text[start..index].Replace("$", string.Empty, StringComparison.Ordinal));
        }
        return tokens;
    }

    private sealed class Reader(List<string> tokens, TableFormulaParser owner)
    {
        private const int MaximumDepth = 256;

        private const int MaximumColumnLetters = 4;

        private int _index;

        private int _depth;

        public bool AtEnd => _index >= tokens.Count;

        private string? Peek => _index < tokens.Count ? tokens[_index] : null;

        private string Next() => _index < tokens.Count ? tokens[_index++] : throw new TableFormulaException("Formula ends too early");

        public FormulaNode Expression()
        {
            var node = Term();
            while (Peek is "+" or "-") node = new BinaryNode(Next()[0], node, Term());
            return node;
        }

        private FormulaNode Term()
        {
            var node = Factor();
            while (Peek is "*" or "/") node = new BinaryNode(Next()[0], node, Factor());
            return node;
        }

        private FormulaNode Factor()
        {
            if (++_depth > MaximumDepth) throw new TableFormulaException("Table formula is nested too deeply");
            try
            {
                return Primary();
            }
            finally
            {
                _depth--;
            }
        }

        private FormulaNode Primary()
        {
            var token = Next();
            if (token is "-" or "+") return new UnaryNode(token[0], Factor());
            if (token == "(")
            {
                var inner = Expression();
                Expect(")");
                return inner;
            }
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return new NumberNode(number);
            if (owner.Directions.TryGetValue(token, out var direction)) return new DirectionNode(direction);
            if (owner.Functions.TryGetValue(token, out var function)) return new FunctionNode(function, Arguments());
            if (Cell(token) is { } reference) return reference;
            throw new TableFormulaException("Unknown name in table formula");
        }

        private IReadOnlyList<FormulaNode> Arguments()
        {
            var arguments = new List<FormulaNode>();
            if (Peek == "(")
            {
                Next();
                if (Peek == ")")
                {
                    Next();
                    return arguments;
                }
                arguments.Add(Expression());
                while (Peek is "," or ";" or "|")
                {
                    Next();
                    arguments.Add(Expression());
                }
                Expect(")");
                return arguments;
            }
            arguments.Add(Factor());
            while (Peek == "|")
            {
                Next();
                arguments.Add(Factor());
            }
            return arguments;
        }

        private void Expect(string token)
        {
            if (Next() != token) throw new TableFormulaException("Expected " + token);
        }

        private FormulaNode? Cell(string token)
        {
            var parts = token.Split(':');
            if (parts.Length == 1) return Address(parts[0]) is { } single ? new ReferenceNode(single.Row, single.Column) : null;
            if (parts.Length == 2 && Address(parts[0]) is { } from && Address(parts[1]) is { } to)
                return new RangeNode(new TableCellRange(from.Row, from.Column, to.Row, to.Column));
            return null;
        }

        private (int Row, int Column)? Address(string text)
        {
            var letters = 0;
            while (letters < text.Length && char.IsAsciiLetter(text[letters])) letters++;
            if (letters == 0 || letters == text.Length || letters > MaximumColumnLetters) return null;
            if (!int.TryParse(text[letters..], NumberStyles.None, CultureInfo.InvariantCulture, out var row) || row < 1) return null;
            var column = 0;
            foreach (var letter in text[..letters].ToUpperInvariant()) column = column * 26 + (letter - 'A' + 1);
            return (row - 1, column - 1);
        }
    }
}

public interface ITableFormulaSyntax
{
    string Name { get; }

    string Print(FormulaNode node, ITableFormulaContext? context);
}

public abstract class TableFormulaSyntax : ITableFormulaSyntax, IFormulaNodeVisitor<string>
{
    private ITableFormulaContext? _context;

    public abstract string Name { get; }

    public string Print(FormulaNode node, ITableFormulaContext? context)
    {
        _context = context;
        return node.Accept(this);
    }

    protected ITableFormulaContext? Context => _context;

    protected string Address(int row, int column)
    {
        var letters = new StringBuilder();
        for (var value = column + 1; value > 0; value = (value - 1) / 26) letters.Insert(0, (char)('A' + (value - 1) % 26));
        return letters.ToString() + (row + 1).ToString(CultureInfo.InvariantCulture);
    }

    public string VisitNumber(NumberNode node) => node.Value.ToString("R", CultureInfo.InvariantCulture);

    public abstract string VisitReference(ReferenceNode node);

    public abstract string VisitRange(RangeNode node);

    public abstract string VisitDirection(DirectionNode node);

    public string VisitUnary(UnaryNode node) => node.Sign + Operand(node.Operand);

    public string VisitBinary(BinaryNode node)
    {
        var left = node.Left.Accept(this);
        var right = node.Right.Accept(this);
        if (node.Left is BinaryNode { Operation: '+' or '-' } && node.Operation is '*' or '/') left = "(" + left + ")";
        if (node.Right is BinaryNode inner && (node.Operation is '*' or '/' || node.Operation == '-' && inner.Operation is '+' or '-')) right = "(" + right + ")";
        return left + node.Operation + right;
    }

    public abstract string VisitFunction(FunctionNode node);

    private string Operand(FormulaNode node) => node is BinaryNode ? "(" + node.Accept(this) + ")" : node.Accept(this);
}

public sealed class CanonicalFormulaSyntax : TableFormulaSyntax
{
    public override string Name => "canonical";

    public override string VisitReference(ReferenceNode node) => Address(node.Row, node.Column);

    public override string VisitRange(RangeNode node) => Address(node.Range.Top, node.Range.Left) + ":" + Address(node.Range.Bottom, node.Range.Right);

    public override string VisitDirection(DirectionNode node) => node.Direction.ToString().ToUpperInvariant();

    public override string VisitFunction(FunctionNode node) =>
        node.Function.ToString().ToUpperInvariant() + "(" + string.Join(",", node.Arguments.Select(argument => argument.Accept(this))) + ")";
}

public sealed class WriterFormulaSyntax : TableFormulaSyntax
{
    public override string Name => "writer";

    public override string VisitReference(ReferenceNode node) => "<" + Address(node.Row, node.Column) + ">";

    public override string VisitRange(RangeNode node) => "<" + Address(node.Range.Top, node.Range.Left) + ":" + Address(node.Range.Bottom, node.Range.Right) + ">";

    public override string VisitDirection(DirectionNode node) =>
        Context is { } context && node.Resolve(context) is { } range ? VisitRange(new RangeNode(range)) : "0";

    public override string VisitFunction(FunctionNode node)
    {
        var name = node.Function == FormulaFunction.Average ? "mean" : node.Function.ToString().ToLowerInvariant();
        var arguments = node.Arguments.Select(argument => argument is NumberNode or ReferenceNode or RangeNode or DirectionNode
            ? argument.Accept(this)
            : "(" + argument.Accept(this) + ")");
        return name + " " + string.Join("|", arguments);
    }
}

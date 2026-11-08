using System.Text;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public sealed record PivotFormula(FormulaNode? Expression, IReadOnlyList<int> Columns)
{
    public bool IsValid => Expression is not null;
}

public interface IPivotFormulas
{
    PivotFormula Compile(string formula, IReadOnlyList<string> headers);

    CellValue Evaluate(PivotFormula formula, IReadOnlyList<double> sums);
}

public sealed class PivotFormulas(IFormulaExpressionParser parser) : IPivotFormulas
{
    private const string Placeholder = "QPFIELD";

    public PivotFormula Compile(string formula, IReadOnlyList<string> headers)
    {
        ArgumentNullException.ThrowIfNull(formula);
        ArgumentNullException.ThrowIfNull(headers);

        var text = formula.TrimStart().TrimStart('=');
        var used = new List<int>();
        var rewritten = Substitute(text, headers, used);

        try
        {
            return new PivotFormula(parser.Parse("=" + rewritten), used);
        }
        catch (FormulaSyntaxException)
        {
            return new PivotFormula(null, []);
        }
    }

    public CellValue Evaluate(PivotFormula formula, IReadOnlyList<double> sums)
    {
        if (formula.Expression is null) return CellValue.FromError(CellError.Value);

        var arithmetic = new Arithmetic(sums);
        var result = formula.Expression.Accept(arithmetic);
        if (arithmetic.DividedByZero) return CellValue.FromError(CellError.DivideByZero);
        if (double.IsNaN(result)) return CellValue.FromError(CellError.Value);
        return double.IsFinite(result) ? CellValue.Number(result) : CellValue.FromError(CellError.Number);
    }

    private string Substitute(string text, IReadOnlyList<string> headers, List<int> used)
    {
        var order = Enumerable.Range(0, headers.Count)
            .Where(index => headers[index].Length > 0)
            .OrderByDescending(index => headers[index].Length)
            .ToList();
        var output = new StringBuilder();
        var position = 0;

        while (position < text.Length)
        {
            var current = text[position];

            if (current == '"')
            {
                var end = text.IndexOf('"', position + 1);
                end = end < 0 ? text.Length - 1 : end;
                output.Append(text, position, end - position + 1);
                position = end + 1;
                continue;
            }

            if (current == '\'')
            {
                var end = text.IndexOf('\'', position + 1);
                var name = end < 0 ? text[(position + 1)..] : text[(position + 1)..end];
                var index = IndexOf(headers, name);
                output.Append(index < 0 ? text[position..(end < 0 ? text.Length : end + 1)] : Token(index, used));
                position = end < 0 ? text.Length : end + 1;
                continue;
            }

            var startsWord = position == 0 || !IsWordCharacter(text[position - 1]);
            var match = startsWord
                ? order.FirstOrDefault(index => Matches(text, position, headers[index]), -1)
                : -1;

            if (match >= 0)
            {
                output.Append(Token(match, used));
                position += headers[match].Length;
                continue;
            }

            output.Append(current);
            position++;
        }

        return output.ToString();
    }

    private bool Matches(string text, int position, string header)
    {
        if (string.Compare(text, position, header, 0, header.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
        var after = position + header.Length;
        return after >= text.Length || !IsWordCharacter(text[after]);
    }

    private int IndexOf(IReadOnlyList<string> headers, string name)
    {
        for (var index = 0; index < headers.Count; index++)
            if (string.Equals(headers[index], name, StringComparison.OrdinalIgnoreCase)) return index;
        return -1;
    }

    private bool IsWordCharacter(char character) => char.IsLetterOrDigit(character) || character == '_';

    private string Token(int index, List<int> used)
    {
        var slot = used.IndexOf(index);
        if (slot < 0)
        {
            used.Add(index);
            slot = used.Count - 1;
        }
        return Placeholder + ToLetters(slot);
    }

    private string ToLetters(int slot)
    {
        var letters = new StringBuilder();
        do
        {
            letters.Insert(0, (char)('A' + slot % 26));
            slot = slot / 26 - 1;
        }
        while (slot >= 0);
        return "X" + letters;
    }

    private sealed class Arithmetic(IReadOnlyList<double> sums) : FormulaNodeVisitor<double>
    {
        public bool DividedByZero { get; private set; }

        public override double VisitLiteral(LiteralNode node) => node.Value.Kind switch
        {
            ValueKind.Number => node.Value.AsNumber(),
            ValueKind.Boolean => node.Value.AsBoolean() ? 1 : 0,
            _ => double.NaN,
        };

        public override double VisitName(NameNode node) =>
            node.Name.StartsWith(Placeholder, StringComparison.OrdinalIgnoreCase) && Slot(node.Name[Placeholder.Length..]) is var slot && slot >= 0 && slot < sums.Count
                ? sums[slot]
                : double.NaN;

        public override double VisitUnary(UnaryNode node)
        {
            var operand = node.Operand.Accept(this);
            return node.Operator switch
            {
                UnaryOperator.Minus => -operand,
                UnaryOperator.Percent => operand / 100,
                _ => operand,
            };
        }

        public override double VisitBinary(BinaryNode node)
        {
            var left = node.Left.Accept(this);
            var right = node.Right.Accept(this);
            return node.Operator switch
            {
                BinaryOperator.Add => left + right,
                BinaryOperator.Subtract => left - right,
                BinaryOperator.Multiply => left * right,
                BinaryOperator.Divide => right == 0 ? Zero() : left / right,
                BinaryOperator.Power => Math.Pow(left, right),
                BinaryOperator.Equal => Truth(left == right),
                BinaryOperator.NotEqual => Truth(left != right),
                BinaryOperator.Less => Truth(left < right),
                BinaryOperator.LessOrEqual => Truth(left <= right),
                BinaryOperator.Greater => Truth(left > right),
                BinaryOperator.GreaterOrEqual => Truth(left >= right),
                _ => double.NaN,
            };
        }

        private double Truth(bool value) => value ? 1 : 0;

        private double Zero()
        {
            DividedByZero = true;
            return double.NaN;
        }

        protected override double Otherwise(FormulaNode node) => double.NaN;

        private int Slot(string letters)
        {
            if (letters.Length < 2 || letters[0] is not ('X' or 'x')) return -1;
            var slot = 0;
            foreach (var letter in letters[1..].ToUpperInvariant())
            {
                if (letter is < 'A' or > 'Z') return -1;
                slot = slot * 26 + (letter - 'A' + 1);
            }
            return slot - 1;
        }
    }
}
